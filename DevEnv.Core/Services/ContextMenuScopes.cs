using DevEnv.Core.Models;

namespace DevEnv.Core.Services;

/// <summary>自定义右键菜单项到注册表作用域的映射。纯函数，便于单测。</summary>
public static class ContextMenuScopes
{
    public const string Files = "*";
    public const string Directories = "Directory";
    public const string DirectoryBackground = @"Directory\Background";
    public const string DesktopBackground = "DesktopBackground";

    /// <summary>取得条目应写入的注册表作用域；顺序固定。</summary>
    public static IReadOnlyList<string> GetScopes(ContextMenuEntry entry)
    {
        var list = new List<string>(4);
        if (entry.ForFiles) list.Add(Files);
        if (entry.ForDirectories) list.Add(Directories);
        if (entry.ForDirectoryBackground) list.Add(DirectoryBackground);
        if (entry.ForDesktop) list.Add(DesktopBackground);
        return list;
    }
}
