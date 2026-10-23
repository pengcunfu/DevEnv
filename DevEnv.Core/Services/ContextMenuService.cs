using System.Diagnostics;
using System.IO;
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
/// 配置路径沿用旧版独立的 MenuManager 工具
/// （文档\FNSoftware\MenuManager\menu_configs.json），老用户配置和旧版安装目录下的
/// menu_configs.json 会自动迁移。
/// </summary>
public sealed class ContextMenuService
{
    public ContextMenuService()
    {
        MigrateLegacyConfigIfNeeded();
    }

    /// <summary>旧版把 menu_configs.json 放在程序目录；首次运行时并入用户数据目录。</summary>
    private static void MigrateLegacyConfigIfNeeded()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
                return;

            var installDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            var legacyPath = Path.Combine(installDir, ConfigFileName);
            if (!File.Exists(legacyPath))
                return;

            Directory.CreateDirectory(UserDataDirectory);
            File.Copy(legacyPath, ConfigFilePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"迁移右键菜单配置失败：{ex.Message}");
        }
    }

    /// <summary>提权子进程的命令行参数。</summary>
    public const string ElevatedArgument = "--context-menu-apply";

    private const string Win11MenuKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";

    private const string ConfigFileName = "menu_configs.json";

    private static readonly object Gate = new();

    public static string UserDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "FNSoftware",
        "MenuManager");

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
        if (!entry.ForFiles && !entry.ForDirectories && !entry.ForDesktop)
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

        return IsAdmin()
            ? WriteEntry(entry)
            : RunElevated(ElevatedOperation.Add, entry);
    }

    public OperationResult RemoveEntryFromSystem(ContextMenuEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Root))
            return OperationResult.Fail("条目缺少注册表键名");
        return IsAdmin()
            ? DeleteEntryKeys(entry)
            : RunElevated(ElevatedOperation.Remove, entry);
    }

    private OperationResult WriteEntry(ContextMenuEntry entry)
    {
        try
        {
            foreach (var scope in ScopesOf(entry))
                WriteScope(entry, scope);
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
            RestartExplorer();
            return OperationResult.Success("已从注册表移除");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"移除失败：{ex.Message}");
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

    /// <summary>条目对应的注册表 scope：*（文件）、Directory（文件夹）、Directory\Background（桌面）。</summary>
    private static IEnumerable<string> ScopesOf(ContextMenuEntry entry)
    {
        if (entry.ForFiles)
            yield return "*";
        if (entry.ForDirectories)
            yield return "Directory";
        if (entry.ForDesktop)
            yield return @"Directory\Background";
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
    }

    private sealed class ElevatedRequest
    {
        [JsonProperty("operation")]
        public string Operation { get; set; } = string.Empty;

        [JsonProperty("entry")]
        public ContextMenuEntry Entry { get; set; } = new();

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

    private static OperationResult RunElevated(ElevatedOperation operation, ContextMenuEntry entry)
    {
        var tag = Guid.NewGuid().ToString("N");
        var requestFile = Path.Combine(Path.GetTempPath(), $"devenv_ctxmenu_{tag}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"devenv_ctxmenu_result_{tag}.json");

        try
        {
            File.WriteAllText(requestFile, JsonConvert.SerializeObject(new ElevatedRequest
            {
                Operation = operation.ToString(),
                Entry = entry,
                ResultFile = resultFile,
            }));

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
            return result is { Success: true }
                ? OperationResult.Success(operation == ElevatedOperation.Add ? "已写入注册表" : "已从注册表移除")
                : OperationResult.Fail(result?.Error ?? "操作失败");
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
                WriteOrDelete(operation, request.Entry);
            else
                throw new InvalidOperationException($"不支持的操作：{request.Operation}");

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
        RestartExplorer();
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