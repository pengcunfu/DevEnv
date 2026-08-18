using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Linq;
using System.Threading.Tasks;
using WinBootSelfStarting.Models;

namespace WinBootSelfStarting.Services
{
    public static class StartupManager
    {
        private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string DisabledKey = "Software\\WinBootSelfStarting\\DisabledRun";

        private static string StartupFolderPath => Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        private static string DisabledStartupFolder => Path.Combine(Path.GetDirectoryName(StartupFolderPath) ?? StartupFolderPath, "WinBootSelfStarting_Disabled");

        public static async Task<List<StartupEntry>> ListEntriesAsync()
        {
            var list = new List<StartupEntry>();

            // Load registry and folder entries synchronously (fast)
            list.AddRange(ListRegistryEntries());

            // Load services and tasks in parallel (slow)
            var tasks = new List<Task<List<StartupEntry>>>
            {
                Task.Run(() => ListAutoStartServices()),
                Task.Run(() => ListScheduledTasksAsync().GetAwaiter().GetResult())
            };

            var results = await Task.WhenAll(tasks);
            list.AddRange(results.SelectMany(r => r));

            return list;
        }

        public static List<StartupEntry> ListEntries()
        {
            return ListEntriesAsync().GetAwaiter().GetResult();
        }

        private static List<StartupEntry> ListRegistryEntries()
        {
            var list = new List<StartupEntry>();

            // Registry enabled
            using (var hkcu = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                if (hkcu != null)
                {
                    foreach (var name in hkcu.GetValueNames())
                    {
                        var val = Convert.ToString(hkcu.GetValue(name)) ?? "";
                        list.Add(new StartupEntry { Id = name, Name = name, Command = val, Location = StartupLocation.Registry });
                    }
                }
            }

            // Registry disabled (our disabled store)
            using (var dk = Registry.CurrentUser.OpenSubKey(DisabledKey, false))
            {
                if (dk != null)
                {
                    foreach (var name in dk.GetValueNames())
                    {
                        var val = Convert.ToString(dk.GetValue(name)) ?? "";
                        list.Add(new StartupEntry { Id = name, Name = name, Command = val, Location = StartupLocation.DisabledRegistry });
                    }
                }
            }

            // Startup folder enabled
            try
            {
                if (Directory.Exists(StartupFolderPath))
                {
                    foreach (var f in Directory.GetFiles(StartupFolderPath))
                    {
                        var fi = new FileInfo(f);
                        list.Add(new StartupEntry { Id = fi.Name, Name = fi.Name, Command = fi.FullName, Location = StartupLocation.StartupFolder });
                    }
                }
            }
            catch { }

            // Disabled startup files
            try
            {
                if (Directory.Exists(DisabledStartupFolder))
                {
                    foreach (var f in Directory.GetFiles(DisabledStartupFolder))
                    {
                        var fi = new FileInfo(f);
                        list.Add(new StartupEntry { Id = fi.Name, Name = fi.Name, Command = fi.FullName, Location = StartupLocation.DisabledFolder });
                    }
                }
            }
            catch { }

            return list;
        }

        private static List<StartupEntry> ListAutoStartServices()
        {
            var list = new List<StartupEntry>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service"))
                {
                    foreach (ManagementObject service in searcher.Get())
                    {
                        var startMode = service["StartMode"]?.ToString();
                        var name = service["Name"]?.ToString() ?? "";
                        var displayName = service["DisplayName"]?.ToString() ?? "";
                        var pathName = service["PathName"]?.ToString() ?? "";
                        var state = service["State"]?.ToString() ?? "";

                        // Only include auto-start services
                        if (startMode == "Auto" || startMode == "Automatic")
                        {
                            list.Add(new StartupEntry
                            {
                                Id = name,
                                Name = displayName,
                                Command = pathName,
                                Location = StartupLocation.Service,
                                ServiceStatus = state,
                                StartType = startMode
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        private static async Task<List<StartupEntry>> ListScheduledTasksAsync()
        {
            var list = new List<StartupEntry>();
            try
            {
                // Use schtasks.exe to list scheduled tasks
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
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
                process.WaitForExit();

                // Parse CSV output
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2) return list;

                // Skip header line, filter tasks quickly
                var taskNames = new List<string>();
                var taskStatuses = new Dictionary<string, string>();

                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = ParseCsvLine(lines[i]);
                    if (parts.Length < 2) continue;

                    var taskName = parts[0].Trim('"');
                    var status = parts.Length > 3 ? parts[3].Trim('"') : "";
                    taskNames.Add(taskName);
                    taskStatuses[taskName] = status;
                }

                // Get task details in parallel (but limit concurrency)
                var detailTasks = taskNames.Select(name => Task.Run(() => new { Name = name, Details = GetTaskDetailsFast(name) }));
                var details = await Task.WhenAll(detailTasks);

                foreach (var detail in details)
                {
                    if (detail.Details != null && (detail.Details.Contains("LOGON", StringComparison.OrdinalIgnoreCase) ||
                                                   detail.Details.Contains("STARTUP", StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(new StartupEntry
                        {
                            Id = detail.Name,
                            Name = detail.Name,
                            Command = detail.Details,
                            Location = StartupLocation.ScheduledTask,
                            ServiceStatus = taskStatuses[detail.Name]
                        });
                    }
                }
            }
            catch { }
            return list;
        }

        private static List<StartupEntry> ListScheduledTasks()
        {
            return ListScheduledTasksAsync().GetAwaiter().GetResult();
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = "";
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
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
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
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

        private static string? GetTaskDetails(string taskName)
        {
            return GetTaskDetailsFast(taskName);
        }

        public static bool AddRegistryEntry(string name, string command)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null)
                    {
                        using (var created = Registry.CurrentUser.CreateSubKey(RunKey))
                        {
                            created.SetValue(name, command);
                        }
                    }
                    else
                    {
                        key.SetValue(name, command);
                    }
                }
                return true;
            }
            catch { return false; }
        }

        public static bool RemoveEntry(StartupEntry entry)
        {
            try
            {
                switch (entry.Location)
                {
                    case StartupLocation.Registry:
                        using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                        {
                            key?.DeleteValue(entry.Id, false);
                        }
                        break;
                    case StartupLocation.DisabledRegistry:
                        using (var dk = Registry.CurrentUser.OpenSubKey(DisabledKey, true))
                        {
                            dk?.DeleteValue(entry.Id, false);
                        }
                        break;
                    case StartupLocation.StartupFolder:
                        if (File.Exists(entry.Command)) File.Delete(entry.Command);
                        break;
                    case StartupLocation.DisabledFolder:
                        if (File.Exists(entry.Command)) File.Delete(entry.Command);
                        break;
                    case StartupLocation.Service:
                        return DeleteService(entry.Id);
                    case StartupLocation.ScheduledTask:
                        return DeleteScheduledTask(entry.Id);
                }
                return true;
            }
            catch { return false; }
        }

        public static bool DisableEntry(StartupEntry entry)
        {
            try
            {
                switch (entry.Location)
                {
                    case StartupLocation.Registry:
                        // Move to our disabled key
                        using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                        {
                            var val = key?.GetValue(entry.Id);
                            if (val != null && key != null)
                            {
                                using (var dk = Registry.CurrentUser.CreateSubKey(DisabledKey))
                                {
                                    dk.SetValue(entry.Id, val);
                                }
                                key.DeleteValue(entry.Id, false);
                            }
                        }
                        break;
                    case StartupLocation.StartupFolder:
                        Directory.CreateDirectory(DisabledStartupFolder);
                        var dest = Path.Combine(DisabledStartupFolder, Path.GetFileName(entry.Command));
                        if (File.Exists(entry.Command)) File.Move(entry.Command, dest, true);
                        break;
                    case StartupLocation.Service:
                        return DisableService(entry.Id);
                    case StartupLocation.ScheduledTask:
                        return DisableScheduledTask(entry.Id);
                    default:
                        // already disabled or unknown
                        break;
                }
                return true;
            }
            catch { return false; }
        }

        public static bool EnableEntry(StartupEntry entry)
        {
            try
            {
                switch (entry.Location)
                {
                    case StartupLocation.DisabledRegistry:
                        using (var dk = Registry.CurrentUser.OpenSubKey(DisabledKey, true))
                        {
                            var val = dk?.GetValue(entry.Id);
                            if (val != null && dk != null)
                            {
                                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                                {
                                    key.SetValue(entry.Id, val);
                                }
                                dk.DeleteValue(entry.Id, false);
                            }
                        }
                        break;
                    case StartupLocation.DisabledFolder:
                        var src = entry.Command;
                        if (File.Exists(src))
                        {
                            Directory.CreateDirectory(StartupFolderPath);
                            var dest = Path.Combine(StartupFolderPath, Path.GetFileName(src));
                            File.Move(src, dest, true);
                        }
                        break;
                    default:
                        // Services and scheduled tasks cannot be enabled (they're already running)
                        // already enabled or unknown
                        break;
                }
                return true;
            }
            catch { return false; }
        }

        // Service management methods
        private static bool DisableService(string serviceName)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sc",
                        Arguments = $"config \"{serviceName}\" start= demand",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };

                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch { return false; }
        }

        private static bool DeleteService(string serviceName)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sc",
                        Arguments = $"delete \"{serviceName}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };

                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch { return false; }
        }

        // Scheduled task management methods
        private static bool DisableScheduledTask(string taskName)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Change /TN \"{taskName}\" /DISABLE",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch { return false; }
        }

        private static bool DeleteScheduledTask(string taskName)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Delete /TN \"{taskName}\" /F",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch { return false; }
        }
    }
}
