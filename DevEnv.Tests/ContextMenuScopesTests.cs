using DevEnv.Core.Models;
using DevEnv.Core.Services;
using Newtonsoft.Json;

namespace DevEnv.Tests;

public sealed class ContextMenuScopesTests
{
    [Fact]
    public void GetScopes_MapsEveryScopeToItsRegistryPath()
    {
        var entry = new ContextMenuEntry
        {
            ForFiles = true,
            ForDirectories = true,
            ForDirectoryBackground = true,
            ForDesktop = true,
        };

        var scopes = ContextMenuScopes.GetScopes(entry);

        Assert.Equal(
            ["*", "Directory", @"Directory\Background", "DesktopBackground"],
            scopes);
    }

    [Fact]
    public void GetScopes_DesktopAndFolderBackgroundAreDistinct()
    {
        var desktop = ContextMenuScopes.GetScopes(new ContextMenuEntry { ForDesktop = true });
        var background = ContextMenuScopes.GetScopes(new ContextMenuEntry { ForDirectoryBackground = true });

        Assert.Equal(["DesktopBackground"], desktop);
        Assert.Equal([@"Directory\Background"], background);
    }

    [Fact]
    public void GetScopes_ReturnsEmptyWhenNothingSelected()
    {
        Assert.Empty(ContextMenuScopes.GetScopes(new ContextMenuEntry()));
    }

    [Fact]
    public void ScopeText_IncludesDirectoryBackground()
    {
        var entry = new ContextMenuEntry
        {
            ForFiles = true,
            ForDirectoryBackground = true,
            ForDesktop = true,
        };

        Assert.Equal("文件 + 文件夹空白处 + 桌面", entry.ScopeText);
    }

    [Fact]
    public void Json_RoundTripsDirectoryBackground()
    {
        var json = JsonConvert.SerializeObject(new ContextMenuEntry
        {
            Name = "test",
            ForFiles = true,
            ForDirectoryBackground = true,
        });

        var parsed = JsonConvert.DeserializeObject<ContextMenuEntry>(json);

        Assert.NotNull(parsed);
        Assert.True(parsed!.ForFiles);
        Assert.True(parsed.ForDirectoryBackground);
        Assert.False(parsed.ForDesktop);
    }
}
