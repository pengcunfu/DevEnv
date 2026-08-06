using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.Win32;

namespace ProgramBox.Utils
{
    /// <summary>
    /// 应用程序帮助工具类
    /// </summary>
    public static class AppHelper
    {
        /// <summary>
        /// 全局配置文件路径
        /// </summary>
        public static readonly string ConfigPath = GetFullPath("config.json");

        /// <summary>
        /// JSON数据文件路径
        /// </summary>
        public static readonly string JsonPath = GetFullPath("data.json");

        /// <summary>
        /// 获取完整路径
        /// </summary>
        /// <param name="shortPath">相对路径</param>
        /// <returns>完整路径</returns>
        public static string GetFullPath(string shortPath)
        {
            var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var appDirectory = Path.GetDirectoryName(appPath) ?? Environment.CurrentDirectory;
            return Path.Combine(appDirectory, shortPath);
        }

        /// <summary>
        /// 从远程服务器下载文件
        /// </summary>
        /// <param name="url">下载地址</param>
        /// <param name="filePath">保存路径</param>
        /// <returns>是否下载成功</returns>
        public static async Task<bool> DownloadFileAsync(string url, string filePath)
        {
            try
            {
                using var client = new HttpClient();
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(filePath, content);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"下载文件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 在默认浏览器中打开网页
        /// </summary>
        /// <param name="url">网址</param>
        public static void OpenInBrowser(string url)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync($"无法打开浏览器: {ex.Message}");
            }
        }

        /// <summary>
        /// 在资源管理器中打开文件或文件夹
        /// </summary>
        /// <param name="path">文件或文件夹路径</param>
        public static void OpenInExplorer(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    // 选中文件
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else if (Directory.Exists(path))
                {
                    // 打开文件夹
                    Process.Start("explorer.exe", $"\"{path}\"");
                }
                else
                {
                    _ = ShowWarningAsync("指定的路径不存在");
                }
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync($"无法打开资源管理器: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动可执行文件（GUI 程序）
        /// </summary>
        public static void LaunchExecutable(string execPath)
        {
            try
            {
                var fullPath = Path.GetFullPath(execPath);
                if (!File.Exists(fullPath))
                {
                    _ = ShowErrorAsync($"找不到应用程序: {fullPath}");
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync($"启动应用程序失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动进程
        /// </summary>
        /// <param name="fileName">文件名或命令</param>
        /// <param name="arguments">参数</param>
        /// <param name="workingDirectory">工作目录</param>
        /// <returns>进程对象</returns>
        public static Process? StartProcess(string fileName, string? arguments = null, string? workingDirectory = null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    psi.WorkingDirectory = workingDirectory;
                }

                return Process.Start(psi);
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync($"启动进程失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 执行命令并获取输出
        /// </summary>
        /// <param name="command">命令</param>
        /// <param name="workingDirectory">工作目录</param>
        /// <returns>命令输出</returns>
        public static async Task<string> ExecuteCommandAsync(string command, string? workingDirectory = null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    return string.IsNullOrEmpty(error) ? output : $"{output}\nError: {error}";
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"执行命令失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 设置开机自启动
        /// </summary>
        /// <param name="enable">是否启用</param>
        /// <param name="appName">应用程序名称</param>
        public static void SetAutoStart(bool enable, string appName = "ProgramBox")
        {
            try
            {
                const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
                
                if (key != null)
                {
                    if (enable)
                    {
                        var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                        key.SetValue(appName, exePath);
                    }
                    else
                    {
                        key.DeleteValue(appName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync($"设置开机自启动失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否已设置开机自启动
        /// </summary>
        /// <param name="appName">应用程序名称</param>
        /// <returns>是否已设置自启动</returns>
        public static bool IsAutoStartEnabled(string appName = "ProgramBox")
        {
            try
            {
                const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, false);
                return key?.GetValue(appName) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 显示信息消息
        /// </summary>
        public static async Task ShowInfoAsync(string message, string title = "信息")
        {
            var window = GetMainWindow();
            if (window != null)
            {
                var messageBox = new Window
                {
                    Title = title,
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new TextBlock { Text = message, Margin = new Avalonia.Thickness(20) }
                };
                await messageBox.ShowDialog(window);
            }
        }

        /// <summary>
        /// 显示警告消息
        /// </summary>
        public static async Task ShowWarningAsync(string message, string title = "警告")
        {
            await ShowInfoAsync(message, title);
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        public static async Task ShowErrorAsync(string message, string title = "错误")
        {
            await ShowInfoAsync(message, title);
        }

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        public static async Task<bool> ShowConfirmAsync(string message, string title = "确认")
        {
            var window = GetMainWindow();
            if (window != null)
            {
                var messageBox = new Window
                {
                    Title = title,
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                // Simplified - in production use proper dialog
                await messageBox.ShowDialog(window);
                return true;
            }
            return false;
        }

        private static Window? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }
    }
}
