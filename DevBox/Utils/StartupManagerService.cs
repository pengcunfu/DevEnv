using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Win32;
using ProgramBox.Models;

namespace ProgramBox.Utils
{
    /// <summary>
    /// 启动项 / 自启服务 / 计划任务管理（HKCU 与启动文件夹本地操作；服务与任务写操作可临时 UAC）
    /// </summary>
    public static class StartupManagerService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string DisabledKey = @"Software\DevBox\DisabledRun";
        private const string LegacyDisabledKey = @"Software\WinBootSelfStarting\DisabledRun";

        private static string StartupFolderPath =>
            Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        private static string DisabledStartupFolder =>
            Path.Combine(Path.GetDirectoryName(StartupFolderPath) ?? StartupFolderPath, "DevBox_Disabled");

        private static string LegacyDisabledStartupFolder =>
            Path.Combine(Path.GetDirectoryName(StartupFolderPath) ?? StartupFolderPath, "WinBootSelfStarting_Disabled");

        public static async Task<List<StartupEntry>> ListEntriesAsync()
        {
            var list = new List<StartupEntry>();
            list.AddRange(ListRegistryAndFolderEntries());

            var results = await Task.WhenAll(
                Task.Run(ListAutoStartServices),
                ListScheduledTasksAsync());

            list.AddRange(results.SelectMany(r => r));
            return list;
        }

        public static bool AddRegistryEntry(string name, string command)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                key.SetValue(name, command);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool RemoveEntry(StartupEntry entry)
        {
            try
            {
                switch (entry.Location)
                {
                    case StartupLocation.Registry:
                        using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                            key?.DeleteValue(entry.Id, false);
                        break;
                    case StartupLocation.DisabledRegistry:
                        DeleteDisabledRegistryValue(entry.Id);
                        break;
                    case StartupLocation.StartupFolder:
                    case StartupLocation.DisabledFolder:
                        if (File.Exists(entry.Command))
                            File.Delete(entry.Command);
                        break;
                    case StartupLocation.Service:
                        return DeleteService(entry.Id);
                    case StartupLocation.ScheduledTask:
                        return DeleteScheduledTask(entry.Id);
                    default:
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool DisableEntry(StartupEntry entry)
        {
            try
            {
                switch (entry.Location)
                {
                    case StartupLocation.Registry:
                        using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                        {
                            var val = key?.GetValue(entry.Id);
                            if (val == null || key == null)
                                return false;

                            using (var dk = Registry.CurrentUser.CreateSubKey(DisabledKey))
                                dk.SetValue(entry.Id, val);
                            key.DeleteValue(entry.Id, false);
                        }
                        break;
                    case StartupLocation.StartupFolder:
                        Directory.CreateDirectory(DisabledStartupFolder);
                        var dest = Path.Combine(DisabledStartupFolder, Path.GetFileName(entry.Command));
                        if (File.Exists(entry.Command))
                            File.Move(entry.Command, dest, true);
                        break;
                    case StartupLocation.Service:
                        return DisableService(entry.Id);
                    case StartupLocation.ScheduledTask:
                        return DisableScheduledTask(entry.Id);
                    default:
                        break;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool EnableEntry(StartupEntry entry)
        {
            try
            {
                switch (entry.Location)
                {
                    case StartupLocation.DisabledRegistry:
                    {
                        var val = GetDisabledRegistryValue(entry.Id, out var sourceKey);
                        if (val == null || sourceKey == null)
                            return false;

                        using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                            key.SetValue(entry.Id, val);
                        using (var dk = Registry.CurrentUser.OpenSubKey(sourceKey, true))
                            dk?.DeleteValue(entry.Id, false);
                        break;
                    }
                    case StartupLocation.DisabledFolder:
                    {
                        var src = entry.Command;
                        if (!File.Exists(src))
                            return false;

                        Directory.CreateDirectory(StartupFolderPath);
                        var dest = Path.Combine(StartupFolderPath, Path.GetFileName(src));
                        File.Move(src, dest, true);
                        break;
                    }
                    default:
                        // 服务 / 计划任务：原工具不支持一键恢复自启
                        break;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static List<StartupEntry> ListRegistryAndFolderEntries()
        {
            var list = new List<StartupEntry>();

            using (var hkcu = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                if (hkcu != null)
                {
                    foreach (var name in hkcu.GetValueNames())
                    {
                        var val = Convert.ToString(hkcu.GetValue(name)) ?? "";
                        list.Add(new StartupEntry
                        {
                            Id = name,
                            Name = name,
                            Command = val,
                            Location = StartupLocation.Registry
                        });
                    }
                }
            }

            AddDisabledRegistryEntries(list, DisabledKey);
            AddDisabledRegistryEntries(list, LegacyDisabledKey);

            AddFolderEntries(list, StartupFolderPath, StartupLocation.StartupFolder);
            AddFolderEntries(list, DisabledStartupFolder, StartupLocation.DisabledFolder);
            AddFolderEntries(list, LegacyDisabledStartupFolder, StartupLocation.DisabledFolder);

            return list;
        }

        private static void AddDisabledRegistryEntries(List<StartupEntry> list, string keyPath)
        {
            using var dk = Registry.CurrentUser.OpenSubKey(keyPath, false);
            if (dk == null)
                return;

            var existing = new HashSet<string>(
                list.Where(e => e.Location == StartupLocation.DisabledRegistry).Select(e => e.Id),
                StringComparer.OrdinalIgnoreCase);

            foreach (var name in dk.GetValueNames())
            {
                if (!existing.Add(name))
                    continue;

                var val = Convert.ToString(dk.GetValue(name)) ?? "";
                list.Add(new StartupEntry
                {
                    Id = name,
                    Name = name,
                    Command = val,
                    Location = StartupLocation.DisabledRegistry
                });
            }
        }

        private static void AddFolderEntries(List<StartupEntry> list, string folder, StartupLocation location)
        {
            try
            {
                if (!Directory.Exists(folder))
                    return;

                var existing = new HashSet<string>(
                    list.Where(e => e.Location == location).Select(e => e.Id),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var f in Directory.GetFiles(folder))
                {
                    var fi = new FileInfo(f);
                    if (!existing.Add(fi.Name))
                        continue;

                    list.Add(new StartupEntry
                    {
                        Id = fi.Name,
                        Name = fi.Name,
                        Command = fi.FullName,
                        Location = location
                    });
                }
            }
            catch
            {
            }
        }

        private static object? GetDisabledRegistryValue(string id, out string? sourceKey)
        {
            sourceKey = null;
            foreach (var keyPath in new[] { DisabledKey, LegacyDisabledKey })
            {
                using var dk = Registry.CurrentUser.OpenSubKey(keyPath, false);
                var val = dk?.GetValue(id);
                if (val != null)
                {
                    sourceKey = keyPath;
                    return val;
                }
            }

            return null;
        }

        private static void DeleteDisabledRegistryValue(string id)
        {
            foreach (var keyPath in new[] { DisabledKey, LegacyDisabledKey })
            {
                using var dk = Registry.CurrentUser.OpenSubKey(keyPath, true);
                dk?.DeleteValue(id, false);
            }
        }

        private static List<StartupEntry> ListAutoStartServices()
        {
            var list = new List<StartupEntry>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service");
                foreach (ManagementObject service in searcher.Get())
                {
                    using (service)
                    {
                        var startMode = service["StartMode"]?.ToString();
                        if (startMode is not ("Auto" or "Automatic"))
                            continue;

                        list.Add(new StartupEntry
                        {
                            Id = service["Name"]?.ToString() ?? "",
                            Name = service["DisplayName"]?.ToString() ?? "",
                            Command = service["PathName"]?.ToString() ?? "",
                            Location = StartupLocation.Service,
                            ServiceStatus = service["State"]?.ToString() ?? "",
                            StartType = startMode
                        });
                    }
                }
            }
            catch
            {
            }

            return list;
        }

        private static async Task<List<StartupEntry>> ListScheduledTasksAsync()
        {
            var list = new List<StartupEntry>();
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = "/Query /FO CSV /V",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2)
                    return list;

                var taskNames = new List<string>();
                var taskStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (var i = 1; i < lines.Length; i++)
                {
                    var parts = ParseCsvLine(lines[i]);
                    if (parts.Length < 2)
                        continue;

                    var taskName = parts[0].Trim('"');
                    if (string.IsNullOrWhiteSpace(taskName) ||
                        taskName.Equals("TaskName", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var status = parts.Length > 3 ? parts[3].Trim('"') : "";
                    if (!taskStatuses.ContainsKey(taskName))
                    {
                        taskNames.Add(taskName);
                        taskStatuses[taskName] = status;
                    }
                }

                var details = await Task.WhenAll(
                    taskNames.Select(name => Task.Run(() =>
                        (Name: name, Details: GetTaskDetailsFast(name)))));

                foreach (var detail in details)
                {
                    if (detail.Details == null)
                        continue;
                    if (!detail.Details.Contains("LOGON", StringComparison.OrdinalIgnoreCase) &&
                        !detail.Details.Contains("STARTUP", StringComparison.OrdinalIgnoreCase))
                        continue;

                    list.Add(new StartupEntry
                    {
                        Id = detail.Name,
                        Name = detail.Name,
                        Command = detail.Details,
                        Location = StartupLocation.ScheduledTask,
                        ServiceStatus = taskStatuses.GetValueOrDefault(detail.Name, "")
                    });
                }
            }
            catch
            {
            }

            return list;
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = "";
            var inQuotes = false;

            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    current += c;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }

            if (current.Length > 0)
                result.Add(current);

            return result.ToArray();
        }

        private static string? GetTaskDetailsFast(string taskName)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Query /TN \"{taskName}\" /FO LIST /V",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output;
            }
            catch
            {
                return null;
            }
        }

        private static bool DisableService(string serviceName) =>
            RunElevated("sc.exe", $"config \"{serviceName}\" start= demand");

        private static bool DeleteService(string serviceName) =>
            RunElevated("sc.exe", $"delete \"{serviceName}\"");

        private static bool DisableScheduledTask(string taskName) =>
            RunElevated("schtasks.exe", $"/Change /TN \"{taskName}\" /DISABLE");

        private static bool DeleteScheduledTask(string taskName) =>
            RunElevated("schtasks.exe", $"/Delete /TN \"{taskName}\" /F");

        /// <summary>
        /// 需要管理员时临时 UAC；已提升则直接执行。
        /// </summary>
        private static bool RunElevated(string fileName, string arguments)
        {
            try
            {
                var elevated = IsProcessElevated();
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true
                };

                if (elevated)
                {
                    startInfo.UseShellExecute = false;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;
                }
                else
                {
                    startInfo.Verb = "runas";
                    startInfo.UseShellExecute = true;
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                }

                using var process = Process.Start(startInfo);
                if (process == null)
                    return false;

                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // 用户取消 UAC
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsProcessElevated()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
