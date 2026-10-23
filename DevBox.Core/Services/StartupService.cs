using DevBox.Core.Models;
using Microsoft.Win32;

namespace DevBox.Core.Services;

/// <summary>开机启动项：当前用户 Run 注册表 + 启动文件夹。逻辑来自 <c>StartupEndpoints</c>。</summary>
public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public IReadOnlyList<StartupEntry> GetEntries()
    {
        var list = new List<StartupEntry>();

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            if (key is not null)
            {
                foreach (var name in key.GetValueNames())
                {
                    list.Add(new StartupEntry
                    {
                        Id = name,
                        Name = name,
                        Command = key.GetValue(name)?.ToString() ?? string.Empty,
                        Location = "注册表",
                    });
                }
            }
        }
        catch
        {
            // 注册表不可读时只返回启动文件夹部分
        }

        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (Directory.Exists(startupFolder))
        {
            foreach (var file in Directory.EnumerateFiles(startupFolder))
            {
                list.Add(new StartupEntry
                {
                    Id = file,
                    Name = Path.GetFileNameWithoutExtension(file),
                    Command = file,
                    Location = "启动文件夹",
                });
            }
        }

        return list;
    }

    public OperationResult Add(string? name, string? command)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(command))
            return OperationResult.Fail("需要名称与命令");

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            key?.SetValue(name, command);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult Delete(string id)
    {
        try
        {
            if (File.Exists(id))
            {
                File.Delete(id);
                return OperationResult.Success();
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(Uri.UnescapeDataString(id), throwOnMissingValue: false);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}