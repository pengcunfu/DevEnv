using System.Reflection;
using System.Text.Json;
using DevEnv.Core.Updates;

namespace DevEnv.Core.Components;

/// <summary>已安装组件记录。</summary>
public sealed record InstalledComponent(
    string Id,
    string Version,
    string Sha256,
    string Path,
    DateTimeOffset InstalledAt);

/// <summary>
/// 可选组件安装状态。安装根默认是 <c>%LOCALAPPDATA%\DevEnv\Components</c>，
/// 目录布局为 <c>&lt;root&gt;\&lt;id&gt;\&lt;version&gt;\</c>，状态写在 <c>&lt;root&gt;\state.json</c>。
/// </summary>
public sealed class ComponentStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, InstalledComponent> _components =
        new(StringComparer.OrdinalIgnoreCase);

    public ComponentStore(string? rootDirectory = null)
    {
        RootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DevEnv",
                "Components")
            : Path.GetFullPath(rootDirectory);

        Directory.CreateDirectory(RootDirectory);
        DownloadsDirectory = Path.Combine(RootDirectory, ".downloads");
        Load();
    }

    /// <summary>组件安装根目录。</summary>
    public string RootDirectory { get; }

    /// <summary>下载 zip 的缓存目录。</summary>
    public string DownloadsDirectory { get; }

    public string StateFilePath => Path.Combine(RootDirectory, "state.json");

    public IReadOnlyList<InstalledComponent> Installed
    {
        get
        {
            lock (_gate)
                return _components.Values.ToList();
        }
    }

    /// <summary>组件是否已安装，并且关键文件确实存在。</summary>
    public bool IsInstalled(string id)
    {
        ComponentDefinition? definition = ComponentCatalog.Find(id);
        if (definition is null)
            return false;

        // 开发构建 / 非单文件发布时可选 DLL 就在 exe 旁边，视为可用，避免开发时被要求下载组件。
        if (CanUseBaseDirectory && HasKeyFiles(AppContext.BaseDirectory, definition))
            return true;

        InstalledComponent? record = GetRecord(id);
        if (record is null)
            return false;

        return HasKeyFiles(ExpectedDirectory(id, record.Version), definition);
    }

    /// <summary>返回已安装组件目录；未安装或关键文件缺失时返回 null。</summary>
    public string? GetDirectory(string id)
    {
        ComponentDefinition? definition = ComponentCatalog.Find(id);
        if (definition is null)
            return null;
        if (CanUseBaseDirectory && HasKeyFiles(AppContext.BaseDirectory, definition))
            return AppContext.BaseDirectory;

        InstalledComponent? record = GetRecord(id);
        if (record is null)
            return null;

        string directory = ExpectedDirectory(id, record.Version);
        return HasKeyFiles(directory, definition) ? directory : null;
    }

    public string? GetInstalledVersion(string id)
    {
        ComponentDefinition? definition = ComponentCatalog.Find(id);
        if (definition is null)
            return null;
        if (CanUseBaseDirectory && HasKeyFiles(AppContext.BaseDirectory, definition))
            return AppVersionInfo.Display;

        InstalledComponent? record = GetRecord(id);
        return record is not null && HasKeyFiles(ExpectedDirectory(id, record.Version), definition)
            ? record.Version
            : null;
    }

    /// <summary>只有非单文件的 DevEnv 宿主（开发运行 / 完整发布）才把 exe 目录当作组件来源。</summary>
    private static bool CanUseBaseDirectory =>
        string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "DevEnv", StringComparison.OrdinalIgnoreCase)
        && File.Exists(Path.Combine(AppContext.BaseDirectory, "DevEnv.dll"));

    private static bool HasKeyFiles(string directory, ComponentDefinition definition)
    {
        if (!Directory.Exists(directory))
            return false;
        foreach (string file in definition.KeyFiles)
        {
            if (!File.Exists(Path.Combine(directory, file)))
                return false;
        }
        return true;
    }

    public InstalledComponent? GetRecord(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        lock (_gate)
            return _components.TryGetValue(id.Trim(), out InstalledComponent? record) ? record : null;
    }

    /// <summary>组件的最终安装目录（无论是否已安装）。</summary>
    public string ExpectedDirectory(string id, string version) =>
        Path.Combine(RootDirectory, id, version);

    /// <summary>组件状态目录。下载/解压时的暂存目录由调用方管理。</summary>
    public string GetComponentRoot(string id) => Path.Combine(RootDirectory, id);

    public string GetDownloadsDirectory(string id)
    {
        string dir = Path.Combine(DownloadsDirectory, id);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public void RecordInstall(string id, string version, string sha256, string path)
    {
        lock (_gate)
        {
            _components[id] = new InstalledComponent(
                id,
                version,
                sha256,
                Path.GetFullPath(path),
                DateTimeOffset.UtcNow);
            SaveLocked();
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            bool removed = _components.Remove(id);
            if (removed)
                SaveLocked();
            return removed;
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(StateFilePath))
                return;

            string json = File.ReadAllText(StateFilePath);
            StateFile? state = JsonSerializer.Deserialize<StateFile>(json, JsonOptions);
            if (state?.Components is null)
                return;

            foreach ((string id, InstalledComponent? record) in state.Components)
            {
                if (record is null || string.IsNullOrWhiteSpace(record.Version))
                    continue;
                _components[id] = record;
            }
        }
        catch
        {
            // 状态文件损坏时按“未安装”处理，下次安装会重写。
        }
    }

    private void SaveLocked()
    {
        try
        {
            var state = new StateFile
            {
                Components = _components.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase),
            };

            string json = JsonSerializer.Serialize(state, JsonOptions);
            string temp = StateFilePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, StateFilePath, overwrite: true);
        }
        catch
        {
            // 状态写入失败不应影响已解压的组件；下次启动按目录探测。
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private sealed class StateFile
    {
        public Dictionary<string, InstalledComponent>? Components { get; set; }
    }
}
