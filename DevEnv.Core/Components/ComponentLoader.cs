using System.Reflection;
using System.Runtime.Loader;

namespace DevEnv.Core.Components;

/// <summary>
/// 组件运行时加载器。可选组件的托管 DLL 不在基础包目录里，默认
/// <see cref="AssemblyLoadContext"/> 解析失败时按已安装组件目录查找。
/// 原生 DLL 与托管 DLL 放在同一个组件目录，由 Windows 默认规则按导入模块目录解析。
/// </summary>
public static class ComponentLoader
{
    private static readonly object Gate = new();
    private static readonly List<string> Directories = [];
    private static ComponentStore? _store;
    private static bool _initialized;

    public static bool IsInitialized
    {
        get
        {
            lock (Gate)
                return _initialized;
        }
    }

    /// <summary>启动时调用一次：登记已安装组件目录并挂上 AssemblyResolve。</summary>
    public static void Initialize(ComponentStore store)
    {
        lock (Gate)
        {
            _store = store;
            if (_initialized)
                return;
            _initialized = true;
            AssemblyLoadContext.Default.Resolving += OnResolving;
        }

        foreach (InstalledComponent component in store.Installed)
        {
            if (store.IsInstalled(component.Id) && store.GetDirectory(component.Id) is { } directory)
                Register(directory);
        }
    }

    /// <summary>组件安装完成后调用，让新目录后续可被解析。</summary>
    public static void Register(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        string full = Path.GetFullPath(directory);
        lock (Gate)
        {
            if (!Directories.Contains(full, StringComparer.OrdinalIgnoreCase))
                Directories.Add(full);
        }
    }

    public static bool IsAvailable(string id) => _store?.IsInstalled(id) == true;

    /// <summary>在已安装组件目录里找原生 DLL 的完整路径（找不到返回 null）。</summary>
    public static string? FindNativeLibrary(string libraryName)
    {
        string fileName = libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? libraryName
            : libraryName + ".dll";

        string[] directories;
        lock (Gate)
            directories = Directories.ToArray();

        foreach (string directory in directories)
        {
            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name)
    {
        if (string.IsNullOrWhiteSpace(name.Name))
            return null;

        // 已经加载过的直接复用，避免同一程序集从不同目录加载两次。
        foreach (Assembly loaded in context.Assemblies)
        {
            if (string.Equals(loaded.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase))
                return loaded;
        }

        string? shortName = name.Name;
        if (shortName is null)
            return null;

        string[] directories;
        lock (Gate)
            directories = Directories.ToArray();

        foreach (string directory in directories)
        {
            string candidate = Path.Combine(directory, shortName + ".dll");
            if (!File.Exists(candidate))
                continue;

            try
            {
                return context.LoadFromAssemblyPath(candidate);
            }
            catch
            {
                // 版本不匹配或文件损坏时交给默认解析；调用方会显示“组件加载失败”。
            }
        }

        return null;
    }
}
