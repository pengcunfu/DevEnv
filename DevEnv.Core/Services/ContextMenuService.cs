using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using DevEnv.Core.Models;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace DevEnv.Core.Services;

/// <summary>
/// Windows 右键菜单管理：
/// 1) Win11 一级菜单 ↔ 完整二级菜单切换（写 HKCU，不需要管理员）
/// 2) 「用 XXX 打开」右键菜单项的增删（写 HKEY_CLASSES_ROOT，需要管理员，
///    非管理员时以 --context-menu-apply 参数提权重启当前程序执行）
///
/// 配置路径统一放在 DevEnv 用户数据目录
/// （文档\FNSoftware\DevEnv\ContextMenu\menu_configs.json），旧版独立 MenuManager 的
/// 配置和旧版安装目录下的 menu_configs.json 会自动迁移。
/// </summary>
public sealed class ContextMenuService
{
    public ContextMenuService()
    {
        MigrateLegacyConfigIfNeeded();
    }

    /// <summary>旧版配置在文档\FNSoftware\MenuManager 或程序目录；首次运行时并入用户数据目录。</summary>
    private static void MigrateLegacyConfigIfNeeded()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
                return;

            foreach (var legacyPath in GetLegacyConfigPaths())
            {
                if (!File.Exists(legacyPath))
                    continue;

                Directory.CreateDirectory(UserDataDirectory);
                File.Copy(legacyPath, ConfigFilePath);
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"迁移右键菜单配置失败：{ex.Message}");
        }
    }

    private static IEnumerable<string> GetLegacyConfigPaths()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Path.Combine(documents, "FNSoftware", "MenuManager", ConfigFileName);

        var installDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        yield return Path.Combine(installDir, ConfigFileName);
    }

    /// <summary>提权子进程的命令行参数。</summary>
    public const string ElevatedArgument = "--context-menu-apply";

    private const string Win11MenuKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";

    private const string ConfigFileName = "menu_configs.json";

    private static readonly object Gate = new();

    public static string UserDataDirectory { get; } = Path.Combine(
        AppPaths.UserDataDirectory,
        "ContextMenu");

    public static string ConfigFilePath => Path.Combine(UserDataDirectory, ConfigFileName);

    #region 配置读写

    public IReadOnlyList<ContextMenuEntry> GetEntries()
    {
        lock (Gate)
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                    return [];
                var file = JsonConvert.DeserializeObject<ContextMenuConfigFile>(File.ReadAllText(ConfigFilePath));
                return file?.Configs ?? [];
            }
            catch
            {
                return [];
            }
        }
    }

    /// <summary>新增或更新一条配置（index 为 null 表示新增）。</summary>
    public OperationResult SaveEntry(ContextMenuEntry entry, int? index = null)
    {
        var validation = Validate(entry);
        if (!validation.Ok)
            return validation;

        lock (Gate)
        {
            var entries = GetEntries().ToList();
            var updating = index is { } i && i >= 0 && i < entries.Count;

            // 新增或改名时都不能与其它条目的键名重复
            var conflict = entries
                .Where((_, i) => !updating || i != index)
                .Any(e => string.Equals(e.Root, entry.Root, StringComparison.OrdinalIgnoreCase));
            if (conflict)
                return OperationResult.Fail($"注册表键名「{entry.Root}」已存在");

            if (updating)
                entries[index!.Value] = entry;
            else
                entries.Add(entry);

            return WriteAll(entries);
        }
    }

    public OperationResult DeleteEntry(int index)
    {
        lock (Gate)
        {
            var entries = GetEntries().ToList();
            if (index < 0 || index >= entries.Count)
                return OperationResult.Fail("条目不存在");
            entries.RemoveAt(index);
            return WriteAll(entries);
        }
    }

    private OperationResult WriteAll(List<ContextMenuEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(UserDataDirectory);
            var file = new ContextMenuConfigFile { Configs = entries };
            File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(file, Formatting.Indented));
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"保存配置失败：{ex.Message}");
        }
    }

    /// <summary>按显示名称生成不冲突的注册表键名（新增配置时留空自动使用）。</summary>
    public string GenerateUniqueRoot(string name)
    {
        var baseRoot = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (string.IsNullOrEmpty(baseRoot))
            baseRoot = "devenvmenu";

        var used = GetEntries()
            .Select(e => e.Root)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var root = baseRoot;
        var counter = 1;
        while (used.Contains(root))
            root = $"{baseRoot}{counter++}";
        return root;
    }

    private static OperationResult Validate(ContextMenuEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
            return OperationResult.Fail("请填写显示名称");
        if (string.IsNullOrWhiteSpace(entry.Root))
            return OperationResult.Fail("请填写注册表键名");
        if (string.IsNullOrWhiteSpace(entry.Path))
            return OperationResult.Fail("请选择程序");
        if (!File.Exists(entry.Path))
            return OperationResult.Fail($"程序不存在：{entry.Path}");
        if (!entry.ForFiles && !entry.ForDirectories && !entry.ForDirectoryBackground && !entry.ForDesktop)
            return OperationResult.Fail("请至少选择一个作用域");
        return OperationResult.Success();
    }

    #endregion

    #region 注册表：自定义右键菜单项

    public bool IsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>该菜单项当前是否已写入注册表。</summary>
    public bool IsEntryEnabled(ContextMenuEntry entry)
    {
        foreach (var scope in ScopesOf(entry))
        {
            try
            {
                using var key = Registry.ClassesRoot.OpenSubKey($@"{scope}\shell\{entry.Root}");
                if (key is null)
                    return false;
            }
            catch
            {
                return false;
            }
        }
        return ScopesOf(entry).Any();
    }

    /// <summary>写入注册表；非管理员时自动走提权子进程。</summary>
    public OperationResult ApplyEntry(ContextMenuEntry entry)
    {
        var validation = Validate(entry);
        if (!validation.Ok)
            return validation;

        if (IsAdmin())
            return WriteEntry(entry);

        var result = RunElevated(ElevatedOperation.Add, entry);
        if (result.Ok)
            RestartExplorer();
        return result;
    }

    public OperationResult RemoveEntryFromSystem(ContextMenuEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Root))
            return OperationResult.Fail("条目缺少注册表键名");
        if (IsAdmin())
            return DeleteEntryKeys(entry);

        var result = RunElevated(ElevatedOperation.Remove, entry);
        if (result.Ok)
            RestartExplorer();
        return result;
    }

    private OperationResult WriteEntry(ContextMenuEntry entry)
    {
        try
        {
            foreach (var scope in ScopesOf(entry))
                WriteScope(entry, scope);
            CleanupLegacyDesktopScope(entry);
            RestartExplorer();
            return OperationResult.Success("已写入注册表");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"写入注册表失败：{ex.Message}");
        }
    }

    private OperationResult DeleteEntryKeys(ContextMenuEntry entry)
    {
        try
        {
            foreach (var scope in ScopesOf(entry))
                Registry.ClassesRoot.DeleteSubKeyTree($@"{scope}\shell\{entry.Root}", throwOnMissingSubKey: false);
            CleanupLegacyDesktopScope(entry);
            RestartExplorer();
            return OperationResult.Success("已从注册表移除");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"移除失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 旧版把「桌面」作用域写到了 Directory\Background（实际是文件夹空白处）。
    /// 应用到桌面时顺手清掉本程序留下的旧键，避免取消勾选后菜单仍残留在文件夹空白处。
    /// </summary>
    private static void CleanupLegacyDesktopScope(ContextMenuEntry entry)
    {
        if (!entry.ForDesktop || entry.ForDirectoryBackground)
            return;

        try
        {
            Registry.ClassesRoot.DeleteSubKeyTree(
                $@"Directory\Background\shell\{entry.Root}", throwOnMissingSubKey: false);
        }
        catch
        {
            // 清理旧键失败不影响本次写入
        }
    }

    private static void WriteScope(ContextMenuEntry entry, string scope)
    {
        var keyPath = $@"{scope}\shell\{entry.Root}";
        var argument = scope == "*" ? "%1" : "%V";
        using (var key = Registry.ClassesRoot.CreateSubKey(keyPath))
        {
            key.SetValue("", entry.Name);
            key.SetValue("Icon", $"\"{entry.Path}\"");
        }

        using var commandKey = Registry.ClassesRoot.CreateSubKey($@"{keyPath}\command");
        commandKey.SetValue("", $"\"{entry.Path}\" \"{argument}\"");
    }

    /// <summary>条目对应的注册表 scope：*（文件）、Directory（文件夹）、
    /// Directory\Background（文件夹空白处）、DesktopBackground（桌面）。</summary>
    private static IEnumerable<string> ScopesOf(ContextMenuEntry entry) =>
        ContextMenuScopes.GetScopes(entry);

    #endregion

    #region 已有菜单项

    /// <summary>系统右键菜单的作用域；顺序即界面排序。</summary>
    private static readonly (string Scope, string Text)[] SystemScopes =
    [
        ("*", "文件"),
        ("Directory", "文件夹"),
        ("Directory\\Background", "文件夹空白处"),
        ("DesktopBackground", "桌面"),
        ("AllFilesystemObjects", "文件与文件夹"),
    ];

    /// <summary>枚举系统里已注册的右键菜单项：shell 命令项 + shellex COM 扩展。</summary>
    public IReadOnlyList<SystemContextMenuItem> GetSystemEntries()
    {
        var list = new List<SystemContextMenuItem>();
        foreach (var (scope, text) in SystemScopes)
        {
            ReadShellVerbs(scope, text, list);
            ReadContextMenuHandlers(scope, text, list);
        }

        return list;
    }

    private static void ReadShellVerbs(string scope, string scopeText, List<SystemContextMenuItem> list)
    {
        using var shell = Registry.ClassesRoot.OpenSubKey($@"{scope}\shell");
        if (shell is null)
            return;

        foreach (var keyName in shell.GetSubKeyNames())
        {
            using var verb = shell.OpenSubKey(keyName);
            if (verb is null)
                continue;

            var relativePath = $@"Software\Classes\{scope}\shell\{keyName}";
            var source = ExistsInCurrentUser(relativePath) ? "HKCU" : "HKLM";
            var hidden = verb.GetValue("ProgrammaticAccessOnly") is not null
                || keyName.StartsWith(".", StringComparison.Ordinal);
            var disabled = verb.GetValue("LegacyDisable") is not null;
            var systemDisabled = disabled && IsDisabledInMachine(relativePath);
            var extended = verb.GetValue("Extended") is not null;
            var command = ReadVerbCommand(verb, out var commandNote);
            var notes = new List<string>();
            if (hidden) notes.Add("系统隐藏");
            if (systemDisabled) notes.Add("系统级禁用");
            if (extended) notes.Add("Shift 扩展菜单");
            if (commandNote.Length > 0) notes.Add(commandNote);

            list.Add(new SystemContextMenuItem
            {
                Id = $"verb|{scope}|{keyName}",
                Name = ResolveVerbName(verb, keyName),
                KeyName = keyName,
                Scope = scope,
                ScopeText = scopeText,
                Kind = "命令",
                Source = source,
                RegistryPath = BuildRegistryPath(source, relativePath),
                Command = command,
                IconPath = ReadString(verb, "Icon"),
                Enabled = !disabled && !hidden,
                CanToggle = !hidden && !systemDisabled,
                Note = string.Join(" · ", notes),
            });
        }
    }

    private static void ReadContextMenuHandlers(string scope, string scopeText, List<SystemContextMenuItem> list)
    {
        using var handlers = Registry.ClassesRoot.OpenSubKey($@"{scope}\shellex\ContextMenuHandlers");
        if (handlers is null)
            return;

        foreach (var keyName in handlers.GetSubKeyNames())
        {
            using var handler = handlers.OpenSubKey(keyName);
            if (handler is null)
                continue;

            var relativePath = $@"Software\Classes\{scope}\shellex\ContextMenuHandlers\{keyName}";
            var source = ExistsInCurrentUser(relativePath) ? "HKCU" : "HKLM";
            var clsid = ReadString(handler, null);
            if (clsid.Length == 0 && LooksLikeClsid(keyName))
                clsid = keyName;
            var userBlocked = IsHandlerBlocked(clsid, user: true);
            var systemBlocked = IsHandlerBlocked(clsid, user: false);
            var dll = ResolveHandlerDll(clsid);
            var notes = new List<string> { "COM 扩展" };
            if (systemBlocked) notes.Add("系统策略禁用");
            if (string.IsNullOrWhiteSpace(clsid)) notes.Add("缺少 CLSID");

            list.Add(new SystemContextMenuItem
            {
                Id = $"handler|{scope}|{keyName}|{clsid}",
                Name = ResolveHandlerName(clsid, keyName),
                KeyName = keyName,
                Scope = scope,
                ScopeText = scopeText,
                Kind = "COM 扩展",
                Source = source,
                RegistryPath = BuildRegistryPath(source, relativePath),
                Command = string.IsNullOrWhiteSpace(dll) ? clsid : dll,
                Clsid = clsid,
                DllPath = dll,
                Enabled = !userBlocked && !systemBlocked,
                CanToggle = !systemBlocked && !string.IsNullOrWhiteSpace(clsid),
                Note = string.Join(" · ", notes),
            });
        }
    }

    /// <summary>启用 / 禁用已有菜单项。命令项写 LegacyDisable，COM 扩展写 Shell Extensions\Blocked，均在当前用户下完成。</summary>
    public OperationResult SetSystemEntryEnabled(SystemContextMenuItem item, bool enabled)
    {
        if (!item.CanToggle)
            return OperationResult.Fail("该项由系统隐藏或系统策略控制，无法在此切换");

        return item.Kind == "COM 扩展"
            ? SetHandlerEnabled(item, enabled)
            : SetVerbEnabled(item, enabled);
    }

    private static OperationResult SetVerbEnabled(SystemContextMenuItem item, bool enabled)
    {
        var relativePath = $@"Software\Classes\{item.Scope}\shell\{item.KeyName}";
        try
        {
            if (!enabled)
            {
                using var key = Registry.CurrentUser.CreateSubKey(relativePath);
                key?.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
            }
            else
            {
                using (var key = Registry.CurrentUser.OpenSubKey(relativePath, writable: true))
                    key?.DeleteValue("LegacyDisable", throwOnMissingValue: false);

                var empty = false;
                using (var key = Registry.CurrentUser.OpenSubKey(relativePath))
                {
                    if (key is not null)
                        empty = key.SubKeyCount == 0 && key.ValueCount == 0;
                }

                if (empty)
                    Registry.CurrentUser.DeleteSubKeyTree(relativePath, throwOnMissingSubKey: false);

                using var merged = Registry.ClassesRoot.OpenSubKey($@"{item.Scope}\shell\{item.KeyName}");
                if (merged?.GetValue("LegacyDisable") is not null)
                    return OperationResult.Fail("已清除当前用户禁用，但系统级 LegacyDisable 仍生效，需要管理员权限处理");
            }

            RestartExplorer();
            return OperationResult.Success(enabled ? "已启用" : "已禁用");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"切换失败：{ex.Message}");
        }
    }

    private static OperationResult SetHandlerEnabled(SystemContextMenuItem item, bool enabled)
    {
        const string blockedPath = @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";
        try
        {
            if (!enabled)
            {
                using var key = Registry.CurrentUser.CreateSubKey(blockedPath);
                key?.SetValue(item.Clsid, string.Empty, RegistryValueKind.String);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(blockedPath, writable: true);
                key?.DeleteValue(item.Clsid, throwOnMissingValue: false);
            }

            RestartExplorer();
            return OperationResult.Success(enabled ? "已启用" : "已禁用");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"切换失败：{ex.Message}");
        }
    }

    /// <summary>删除已有菜单项：先导出 .reg 备份，再删除注册表键；HKLM 项需要提权。</summary>
    public OperationResult DeleteSystemEntry(SystemContextMenuItem item)
    {
        var backup = TryBackupSystemEntry(item, out var backupPath);
        if (!backup.Ok)
            return backup;

        if (item.Source == "HKCU" || IsAdmin())
            return DeleteSystemEntryCore(item, backupPath);

        var elevated = RunElevated(ElevatedOperation.DeleteSystem, item);
        if (elevated.Ok)
            RestartExplorer();

        return elevated.Ok && backupPath.Length > 0
            ? OperationResult.Success($"{elevated.DisplayMessage}，备份：{backupPath}")
            : elevated;
    }

    private static OperationResult DeleteSystemEntryCore(
        SystemContextMenuItem item,
        string backupPath,
        bool restartExplorer = true)
    {
        try
        {
            var isHandler = item.Kind == "COM 扩展";
            var relativePath = isHandler
                ? $@"Software\Classes\{item.Scope}\shellex\ContextMenuHandlers\{item.KeyName}"
                : $@"Software\Classes\{item.Scope}\shell\{item.KeyName}";

            if (item.Source == "HKCU")
                Registry.CurrentUser.DeleteSubKeyTree(relativePath, throwOnMissingSubKey: false);
            else
                Registry.LocalMachine.DeleteSubKeyTree(relativePath, throwOnMissingSubKey: false);

            // 清掉为 HKLM 项写下的当前用户禁用影子，以及 COM 扩展的屏蔽值。
            Registry.CurrentUser.DeleteSubKeyTree(relativePath, throwOnMissingSubKey: false);
            if (isHandler && !string.IsNullOrWhiteSpace(item.Clsid))
            {
                using var blocked = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", writable: true);
                blocked?.DeleteValue(item.Clsid, throwOnMissingValue: false);
            }

            if (restartExplorer)
                RestartExplorer();
            var suffix = string.IsNullOrWhiteSpace(backupPath) ? string.Empty : $"，备份：{backupPath}";
            return OperationResult.Success($"已删除{suffix}");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"删除失败：{ex.Message}");
        }
    }

    private static OperationResult TryBackupSystemEntry(SystemContextMenuItem item, out string backupPath)
    {
        backupPath = string.Empty;
        try
        {
            var dir = Path.Combine(UserDataDirectory, "backups");
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var safeName = new string(item.KeyName
                .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
                .ToArray());
            if (safeName.Length == 0)
                safeName = "menu";

            var file = Path.Combine(dir, $"{stamp}_{safeName}.reg");
            var info = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"export \"{item.RegistryPath}\" \"{file}\" /y",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(info);
            process?.WaitForExit(15000);
            if (process is null || process.ExitCode != 0 || !File.Exists(file))
                return OperationResult.Fail("导出注册表备份失败，已取消删除");

            backupPath = file;
            return OperationResult.Success(file);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"备份注册表失败：{ex.Message}");
        }
    }

    private static string ReadVerbCommand(RegistryKey verb, out string note)
    {
        note = string.Empty;
        using var commandKey = verb.OpenSubKey("command");
        var command = commandKey?.GetValue(null) as string;
        if (!string.IsNullOrWhiteSpace(command))
            return Environment.ExpandEnvironmentVariables(command.Trim());

        var explorerHandler = ReadString(verb, "ExplorerCommandHandler");
        if (explorerHandler.Length > 0)
        {
            note = "Win11 命令";
            return $"ExplorerCommandHandler: {explorerHandler}";
        }

        var delegateExecute = ReadString(verb, "DelegateExecute");
        if (delegateExecute.Length > 0)
        {
            note = "委托执行";
            return $"DelegateExecute: {delegateExecute}";
        }

        note = "无命令";
        return string.Empty;
    }

    private static string ResolveVerbName(RegistryKey verb, string fallback)
    {
        var resolved = ResolveIndirectString(ReadString(verb, "MUIVerb"));
        if (resolved.Length > 0)
            return resolved;

        resolved = ResolveIndirectString(ReadString(verb, null));
        return resolved.Length > 0 ? resolved : fallback;
    }

    private static string ResolveHandlerName(string clsid, string fallback)
    {
        if (string.IsNullOrWhiteSpace(clsid))
            return fallback;

        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}");
            var resolved = ResolveIndirectString(ReadString(key, null));
            return resolved.Length > 0 ? resolved : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static string ResolveHandlerDll(string clsid)
    {
        if (string.IsNullOrWhiteSpace(clsid))
            return string.Empty;

        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}\InprocServer32");
            var dll = ReadString(key, null).Trim('"');
            return dll.Length > 0 ? Environment.ExpandEnvironmentVariables(dll) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveIndirectString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = Environment.ExpandEnvironmentVariables(value.Trim());
        if (!text.StartsWith('@'))
            return text;

        try
        {
            var buffer = new StringBuilder(512);
            return SHLoadIndirectString(text, buffer, buffer.Capacity, IntPtr.Zero) == 0
                ? buffer.ToString().Trim()
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHLoadIndirectString(
        string source,
        StringBuilder output,
        int outputCapacity,
        IntPtr reserved);

    private static string ReadString(RegistryKey? key, string? name)
    {
        try
        {
            return (key?.GetValue(name) as string)?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool ExistsInCurrentUser(string relativePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(relativePath);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDisabledInMachine(string relativePath)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(relativePath);
            return key?.GetValue("LegacyDisable") is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikeClsid(string value)
    {
        var text = value.Trim();
        if (!text.StartsWith('{') || !text.EndsWith('}'))
            return false;
        return Guid.TryParse(text[1..^1], out _);
    }

    private static string BuildRegistryPath(string source, string relativePath) =>
        $@"{(source == "HKCU" ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE")}\{relativePath}";

    private static bool IsHandlerBlocked(string clsid, bool user)
    {
        if (string.IsNullOrWhiteSpace(clsid))
            return false;

        try
        {
            var root = user ? Registry.CurrentUser : Registry.LocalMachine;
            using var key = root.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked");
            return key?.GetValue(clsid) is not null;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Win11 一级 / 二级菜单切换

    public bool IsWindows11 =>
        Environment.OSVersion.Version.Major == 10 && Environment.OSVersion.Version.Build >= 22000;

    /// <summary>true = 已禁用一级菜单（显示 Win10 风格的完整二级菜单）。</summary>
    public bool IsWin11MenuDisabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Win11MenuKey);
                return key is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>切换 Win11 右键菜单形态；写 HKCU，无需管理员。</summary>
    public OperationResult SetWin11ClassicMenu(bool useClassic)
    {
        if (!IsWindows11)
            return OperationResult.Fail("该功能仅适用于 Windows 11");

        try
        {
            if (useClassic)
            {
                using var key = Registry.CurrentUser.CreateSubKey(Win11MenuKey);
                key.SetValue("", string.Empty, RegistryValueKind.String);
            }
            else
            {
                Registry.CurrentUser.DeleteSubKeyTree(
                    @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", false);
            }

            RestartExplorer();
            return OperationResult.Success(useClassic ? "已切换为完整二级菜单" : "已恢复 Win11 默认一级菜单");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"切换失败：{ex.Message}");
        }
    }

    public string Win11StatusText
    {
        get
        {
            if (!IsWindows11)
                return "当前系统不是 Windows 11，此功能不适用";
            return IsWin11MenuDisabled
                ? "当前：完整二级菜单（Win10 风格）"
                : "当前：Win11 默认一级菜单";
        }
    }

    /// <summary>环境检查摘要：系统版本 / 权限 / 当前形态 / 注册表可达性。</summary>
    public string GetStatusSummary() =>
        $"Win11：{(IsWindows11 ? "是" : "否")} ｜ 管理员：{(IsAdmin() ? "是" : "否")} ｜ " +
        $"{Win11StatusText} ｜ 注册表访问：{(CanAccessRegistry() ? "正常" : "受限")}";

    private static bool CanAccessRegistry()
    {
        const string testKeyPath = @"Software\Classes\CLSID\__test_devenv_access__";
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(testKeyPath))
                key.SetValue("test", "test");
            Registry.CurrentUser.DeleteSubKeyTree(testKeyPath, false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RestartExplorer()
    {
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            try
            {
                process.Kill();
                process.WaitForExit(5000);
            }
            catch
            {
                // 个别进程杀不掉就跳过，最终仍会尝试启动新的
            }
        }
        Process.Start("explorer.exe");
    }

    #endregion

    #region 提权子进程

    private enum ElevatedOperation
    {
        Add,
        Remove,
        DeleteSystem,
    }

    private sealed class ElevatedRequest
    {
        [JsonProperty("operation")]
        public string Operation { get; set; } = string.Empty;

        [JsonProperty("entry")]
        public ContextMenuEntry Entry { get; set; } = new();

        [JsonProperty("system_entry")]
        public SystemContextMenuItem? SystemEntry { get; set; }

        [JsonProperty("result_file")]
        public string ResultFile { get; set; } = string.Empty;
    }

    private sealed class ElevatedResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }
    }

    private static OperationResult RunElevated(ElevatedOperation operation, ContextMenuEntry entry) =>
        RunElevated(operation, request => request.Entry = entry);

    private static OperationResult RunElevated(ElevatedOperation operation, SystemContextMenuItem item) =>
        RunElevated(operation, request => request.SystemEntry = item);

    private static OperationResult RunElevated(
        ElevatedOperation operation,
        Action<ElevatedRequest> configure)
    {
        var tag = Guid.NewGuid().ToString("N");
        var requestFile = Path.Combine(Path.GetTempPath(), $"devenv_ctxmenu_{tag}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"devenv_ctxmenu_result_{tag}.json");

        try
        {
            var request = new ElevatedRequest
            {
                Operation = operation.ToString(),
                ResultFile = resultFile,
            };
            configure(request);
            File.WriteAllText(requestFile, JsonConvert.SerializeObject(request));

            var executable = Environment.ProcessPath;
            if (string.IsNullOrEmpty(executable))
                return OperationResult.Fail("无法确定当前程序路径");

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = $"\"{ElevatedArgument}\" \"{requestFile}\"",
                Verb = "runas",
                UseShellExecute = true,
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();

            if (!File.Exists(resultFile))
                return OperationResult.Fail("未获得管理员权限，操作未执行");

            var result = JsonConvert.DeserializeObject<ElevatedResult>(File.ReadAllText(resultFile));
            if (result is not { Success: true })
                return OperationResult.Fail(result?.Error ?? "操作失败");

            return OperationResult.Success(operation switch
            {
                ElevatedOperation.Add => "已写入注册表",
                ElevatedOperation.Remove => "已从注册表移除",
                _ => "已删除",
            });
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"请求管理员权限失败：{ex.Message}");
        }
        finally
        {
            TryDelete(requestFile);
            TryDelete(resultFile);
        }
    }

    /// <summary>提权子进程入口：读取请求、执行注册表写入、回写结果后退出。</summary>
    public static int ExecuteElevatedRequest(string requestFile)
    {
        ElevatedRequest? request = null;
        try
        {
            request = JsonConvert.DeserializeObject<ElevatedRequest>(File.ReadAllText(requestFile))
                ?? throw new InvalidOperationException("提权请求数据无效");

            if (Enum.TryParse<ElevatedOperation>(request.Operation, out var operation))
            {
                if (operation == ElevatedOperation.DeleteSystem)
                {
                    if (request.SystemEntry is null)
                        throw new InvalidOperationException("缺少要删除的菜单项");
                    var result = DeleteSystemEntryCore(
                        request.SystemEntry, backupPath: string.Empty, restartExplorer: false);
                    if (!result.Ok)
                        throw new InvalidOperationException(result.DisplayMessage);
                }
                else
                {
                    WriteOrDelete(operation, request.Entry);
                }
            }
            else
            {
                throw new InvalidOperationException($"不支持的操作：{request.Operation}");
            }

            WriteResult(request.ResultFile, new ElevatedResult { Success = true });
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                if (request is not null)
                    WriteResult(request.ResultFile, new ElevatedResult { Success = false, Error = ex.Message });
            }
            catch
            {
                // 主进程会自行报告失败
            }
            return 1;
        }
    }

    /// <summary>提权子进程内执行（已是管理员，直接写注册表）。</summary>
    private static void WriteOrDelete(ElevatedOperation operation, ContextMenuEntry entry)
    {
        if (operation == ElevatedOperation.Add)
        {
            foreach (var scope in ScopesOf(entry))
                WriteScope(entry, scope);
        }
        else
        {
            foreach (var scope in ScopesOf(entry))
                Registry.ClassesRoot.DeleteSubKeyTree($@"{scope}\shell\{entry.Root}", false);
        }

        CleanupLegacyDesktopScope(entry);
    }

    private static void WriteResult(string file, ElevatedResult result)
    {
        if (!string.IsNullOrEmpty(file))
            File.WriteAllText(file, JsonConvert.SerializeObject(result));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // 临时文件清不掉不影响功能
        }
    }

    #endregion
}