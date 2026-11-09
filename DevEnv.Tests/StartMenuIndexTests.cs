using DevEnv.Core.Services;

namespace DevEnv.Tests;

public sealed class StartMenuIndexTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "devenv-startmenu-" + Guid.NewGuid().ToString("N"));

    public StartMenuIndexTests() => Directory.CreateDirectory(_tempRoot);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // 临时目录清理失败不影响测试结果
        }
    }

    [Fact]
    public void Build_MergesUserOverCommonAndSkipsExcludedFolders()
    {
        var common = CreateRoot("common");
        var user = CreateRoot("user");

        Write(common, "Root.lnk");
        Write(common, "Office", "Word.lnk");
        Write(common, "Accessories", "System Tools", "Control.lnk");
        Write(common, StartMenuService.ManagedFolderName, "Managed.lnk");
        Write(common, "Startup", "Boot.lnk");

        Write(user, "Root.lnk");
        Write(user, "Office", "Word.lnk");
        Write(user, "Office", "Excel.lnk");

        var items = StartMenuIndex.Build(
            [new StartMenuRoot(common, false), new StartMenuRoot(user, true)],
            StartMenuService.ManagedFolderName,
            ["Startup"]);

        Assert.Equal(4, items.Count);
        Assert.DoesNotContain(items, item => item.Name is "Managed" or "Boot");
        Assert.Contains(items, item => item.Name == "Root" && item.LinkPath == Path.Combine(user, "Root.lnk"));
        Assert.Contains(items, item => item.Name == "Word" && item.LinkPath == Path.Combine(user, "Office", "Word.lnk"));
        Assert.Contains(items, item => item.Name == "Excel");
        Assert.Contains(items, item => item.Name == "Control");
    }

    [Fact]
    public void Build_SplitsFirstLevelFolderAndSubFolder()
    {
        var root = CreateRoot("programs");
        Write(root, "Root.lnk");
        Write(root, "Accessories", "System Tools", "Control.lnk");

        var items = StartMenuIndex.Build(
            [new StartMenuRoot(root, true)],
            StartMenuService.ManagedFolderName,
            ["Startup"]);

        var control = Assert.Single(items, item => item.Name == "Control");
        Assert.Equal("Accessories", control.FirstLevelFolder);
        Assert.Equal("System Tools", control.SubFolder);
        Assert.Equal(Path.Combine("Accessories", "System Tools", "Control.lnk"), control.RelativePath);

        var rootItem = Assert.Single(items, item => item.Name == "Root");
        Assert.Equal(string.Empty, rootItem.FirstLevelFolder);
        Assert.Equal(string.Empty, rootItem.SubFolder);
    }

    [Fact]
    public void Build_MissingRootsAreIgnored()
    {
        var items = StartMenuIndex.Build(
            [new StartMenuRoot(Path.Combine(_tempRoot, "missing"), true)],
            StartMenuService.ManagedFolderName,
            ["Startup"]);

        Assert.Empty(items);
    }

    [Fact]
    public void TryReadInternetShortcut_ReadsUrlWithBom()
    {
        var path = Path.Combine(_tempRoot, "site.url");
        File.WriteAllText(path, "﻿[InternetShortcut]\r\nURL=https://example.com/path\r\n");

        Assert.True(StartMenuIndex.TryReadInternetShortcut(path, out var url));
        Assert.Equal("https://example.com/path", url);
    }

    [Fact]
    public void TryReadInternetShortcut_MissingUrlReturnsFalse()
    {
        var path = Path.Combine(_tempRoot, "empty.url");
        File.WriteAllText(path, "[InternetShortcut]\r\nIconIndex=0\r\n");

        Assert.False(StartMenuIndex.TryReadInternetShortcut(path, out var url));
        Assert.Equal(string.Empty, url);
    }

    [Fact]
    public void ShouldHide_MatchesTargetLinkPathAndUrl()
    {
        var shortcut = new StartMenuItem
        {
            Name = "Demo",
            LinkPath = Path.Combine(_tempRoot, "Demo.lnk"),
            RelativePath = "Demo.lnk",
            TargetPath = Path.Combine(_tempRoot, "Apps", "Demo.exe"),
        };

        var byTarget = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            StartMenuIndex.NormalizePathForCompare(Path.Combine(_tempRoot, "Apps", "DEMO.EXE")),
        };
        Assert.True(StartMenuIndex.ShouldHide(shortcut, byTarget, new HashSet<string>()));

        var byLink = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            StartMenuIndex.NormalizePathForCompare(shortcut.LinkPath),
        };
        Assert.True(StartMenuIndex.ShouldHide(shortcut, byLink, new HashSet<string>()));

        var unrelated = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            StartMenuIndex.NormalizePathForCompare(Path.Combine(_tempRoot, "Other.exe")),
        };
        Assert.False(StartMenuIndex.ShouldHide(shortcut, unrelated, new HashSet<string>()));

        var web = new StartMenuItem
        {
            Name = "Site",
            LinkPath = Path.Combine(_tempRoot, "Site.url"),
            RelativePath = "Site.url",
            Kind = StartMenuLinkKind.InternetShortcut,
            TargetPath = "https://example.com/",
        };
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            StartMenuIndex.NormalizeUrlForCompare("https://EXAMPLE.com"),
        };
        Assert.True(StartMenuIndex.ShouldHide(web, new HashSet<string>(), urls));
    }

    private string CreateRoot(string name)
    {
        var path = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Write(string root, params string[] segments)
    {
        var path = Path.Combine([root, .. segments]);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, string.Empty);
    }
}
