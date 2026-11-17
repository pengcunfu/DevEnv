using System.Diagnostics;
using DevEnv.Core.Models;
using Microsoft.Win32;

namespace DevEnv.Core.Services;

/// <summary>开机启动项：当前用户 Run 注册表 + 启动文件夹。逻辑来自 <c>StartupEndpoints</c>。</summary>
public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyPath = @"HKEY_CURRENT_USER\" + RunKey;

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
                    var command = key.GetValue(name)?.ToString() ?? string.Empty;
                    list.Add(CreateEntry(
                        id: name,
                        name: name,
                        command: command,
                        location: "注册表",
                        locationDetail: RunKeyPath,
                        kind: "注册表值"));
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
            using var links = ShellLinkGateway.Create();
            foreach (var file in Directory.EnumerateFiles(startupFolder))
            {
                // desktop.ini 是文件夹元数据，不是启动项。
                if (string.Equals(Path.GetFileName(file), "desktop.ini", StringComparison.OrdinalIgnoreCase))
                    continue;

                var extension = Path.GetExtension(file);
                var kind = extension.ToLowerInvariant() switch
                {
                    ".lnk" => "快捷方式",
                    ".url" => "网址",
                    _ => "文件",
                };

                var command = file;
                var executablePath = file;
                if (kind == "快捷方式" && links.TryRead(file, out var target, out var arguments))
                {
                    executablePath = StartupCommandParser.ExtractExecutablePath(target);
                    command = string.IsNullOrWhiteSpace(arguments)
                        ? target
                        : $"\"{target}\" {arguments}";
                }

                list.Add(CreateEntry(
                    id: file,
                    name: Path.GetFileNameWithoutExtension(file),
                    command: command,
                    location: "启动文件夹",
                    locationDetail: file,
                    kind: kind,
                    executablePath: executablePath));
            }
        }

        return list;
    }

    private static StartupEntry CreateEntry(
        string id,
        string name,
        string command,
        string location,
        string locationDetail,
        string kind,
        string? executablePath = null)
    {
        executablePath ??= StartupCommandParser.ExtractExecutablePath(command);
        var state = StartupCommandParser.GetFileState(executablePath);
        var info = ReadFileInfo(executablePath);

        return new StartupEntry
        {
            Id = id,
            Name = name,
            Command = command,
            Location = location,
            LocationDetail = locationDetail,
            Kind = kind,
            ExecutablePath = executablePath,
            Description = info.Description,
            Publisher = info.Publisher,
            FileVersion = info.Version,
            FileExists = state switch
            {
                StartupFileState.Exists => true,
                StartupFileState.Missing => false,
                _ => null,
            },
            FileStatusText = state switch
            {
                StartupFileState.Exists => "文件存在",
                StartupFileState.Missing => "文件缺失",
                StartupFileState.Unresolved => "系统命令",
                _ => "未解析",
            },
        };
    }

    private static (string Description, string Publisher, string Version) ReadFileInfo(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return (string.Empty, string.Empty, string.Empty);

        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return (
                info.FileDescription?.Trim() ?? string.Empty,
                info.CompanyName?.Trim() ?? string.Empty,
                info.FileVersion?.Trim() ?? string.Empty);
        }
        catch
        {
            return (string.Empty, string.Empty, string.Empty);
        }
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