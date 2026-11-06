using DevEnv.Core.Models;
using Newtonsoft.Json.Linq;

namespace DevEnv.Tests;

public sealed class LauncherLayoutTests
{
    private static readonly IReadOnlyList<IReadOnlyList<string>> AppsAndFolders =
    [
        ["app|a", "app|b", "app|c", "app|d"],
        ["folder|x"],
        [],
    ];

    [Fact]
    public void Normalize_EmptyLayout_AppendsAllItemsInSourceOrder()
    {
        var layout = new LauncherLayout();
        layout.Normalize(AppsAndFolders);

        Assert.Equal(3, layout.Pages.Count);
        Assert.Equal(
            ["app|a", "app|b", "app|c", "app|d"],
            layout.Pages[0].Tiles.Select(t => t.Key));
        Assert.Equal(["folder|x"], layout.Pages[1].Tiles.Select(t => t.Key));
        Assert.Empty(layout.Pages[2].Tiles);
    }

    [Fact]
    public void Normalize_KeepsGroupAndPrunesRemovedItems()
    {
        var layout = new LauncherLayout();
        var group = new LauncherLayoutTile
        {
            Type = "group",
            Id = "g1",
            Name = "开发",
            Items = ["app|b", "app|missing", "app|c"],
        };
        layout.Page(0).Tiles.Add(LauncherLayoutTile.Item("app|a"));
        layout.Page(0).Tiles.Add(group);

        layout.Normalize(AppsAndFolders);

        var tiles = layout.Pages[0].Tiles;
        Assert.Equal(["app|a", "group|g1", "app|d"], tiles.Select(LauncherLayout.TileId));
        Assert.Equal(["app|b", "app|c"], tiles[1].Items);
        Assert.Equal("开发", tiles[1].Name);
    }

    [Fact]
    public void Normalize_GroupWithSingleItem_IsDowngraded()
    {
        var layout = new LauncherLayout();
        layout.Page(0).Tiles.Add(new LauncherLayoutTile
        {
            Type = "group",
            Id = "g1",
            Name = "只剩一个",
            Items = ["app|b", "app|missing"],
        });

        layout.Normalize(AppsAndFolders);

        var first = layout.Pages[0].Tiles[0];
        Assert.False(first.IsGroup);
        Assert.Equal("app|b", first.Key);
    }

    [Fact]
    public void Normalize_GroupClaimWinsOverIndependentTile()
    {
        var layout = new LauncherLayout();
        layout.Page(0).Tiles.Add(LauncherLayoutTile.Item("app|a"));
        layout.Page(0).Tiles.Add(new LauncherLayoutTile
        {
            Type = "group",
            Id = "g1",
            Name = "组",
            Items = ["app|a", "app|b"],
        });

        layout.Normalize(AppsAndFolders);

        Assert.Equal(["group|g1"], layout.Pages[0].Tiles
            .Take(1)
            .Select(LauncherLayout.TileId));
        Assert.Equal(["app|a", "app|b"], layout.FindGroup("g1")!.Items);
        Assert.DoesNotContain(layout.Pages[0].Tiles,
            tile => !tile.IsGroup && tile.Key.Equals("app|a", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Normalize_DuplicateKey_IsOnlyPlacedOnce()
    {
        var layout = new LauncherLayout();
        layout.Page(0).Tiles.Add(LauncherLayoutTile.Item("app|a"));
        layout.Page(0).Tiles.Add(LauncherLayoutTile.Item("APP|A"));

        layout.Normalize(AppsAndFolders);

        Assert.Single(layout.Pages[0].Tiles, t => t.Key.Equals("app|a", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(4, layout.Pages[0].Tiles.Count);
    }

    [Fact]
    public void MoveTiles_PreservesRelativeOrderAndInsertsAtTarget()
    {
        var layout = new LauncherLayout();
        layout.Normalize(AppsAndFolders);

        layout.MoveTiles(0, ["app|a", "app|b"], 3);

        Assert.Equal(["app|c", "app|a", "app|b", "app|d"], layout.Pages[0].Tiles.Select(t => t.Key));
    }

    [Fact]
    public void MoveTiles_ForwardMoveAdjustsInsertIndex()
    {
        var layout = new LauncherLayout();
        layout.Normalize(AppsAndFolders);

        layout.MoveTiles(0, ["app|a"], 2);

        Assert.Equal(["app|b", "app|a", "app|c", "app|d"], layout.Pages[0].Tiles.Select(t => t.Key));
    }

    [Fact]
    public void CreateGroup_ReplacesItemsWithGroupTile()
    {
        var layout = new LauncherLayout();
        layout.Normalize(AppsAndFolders);

        var group = layout.CreateGroup(0, "常用", ["app|a", "app|c"], 1);

        Assert.NotNull(group);
        var tiles = layout.Pages[0].Tiles;
        Assert.Equal(["group|" + group!.Id, "app|b", "app|d"], tiles.Select(LauncherLayout.TileId));
        Assert.Equal(["app|a", "app|c"], group.Items);
        Assert.Equal("常用", group.Name);
    }

    [Fact]
    public void CreateGroup_WithSingleItem_DoesNothing()
    {
        var layout = new LauncherLayout();
        layout.Normalize(AppsAndFolders);

        Assert.Null(layout.CreateGroup(0, "x", ["app|a"], 0));
        Assert.All(layout.Pages[0].Tiles, tile => Assert.False(tile.IsGroup));
    }

    [Fact]
    public void AddToGroup_RemovesItemsFromPageAndAppendsToGroup()
    {
        var layout = new LauncherLayout();
        layout.Normalize(AppsAndFolders);
        var group = layout.CreateGroup(0, "常用", ["app|a", "app|b"], 0)!;

        layout.AddToGroup(group.Id, ["app|c"]);

        Assert.Equal(["group|" + group.Id, "app|d"], layout.Pages[0].Tiles.Select(LauncherLayout.TileId));
        Assert.Equal(["app|a", "app|b", "app|c"], group.Items);
    }

    [Fact]
    public void MoveOutOfGroup_KeepsGroupWhenTwoRemain()
    {
        var layout = new LauncherLayout();
        layout.Normalize(AppsAndFolders);
        var group = layout.CreateGroup(0, "常用", ["app|a", "app|b", "app|c"], 0)!;

        layout.MoveOutOfGroup(group.Id, ["app|c"]);

        var tiles = layout.Pages[0].Tiles;
        Assert.Equal(["group|" + group.Id, "app|c", "app|d"], tiles.Select(LauncherLayout.TileId));
        Assert.Equal(["app|a", "app|b"], group.Items);
    }

    [Fact]
    public void MoveOutOfGroup_DissolvesWhenLessThanTwoRemain()
    {
        var layout = new LauncherLayout();
        layout.Normalize(AppsAndFolders);
        var group = layout.CreateGroup(0, "常用", ["app|a", "app|b"], 0)!;

        layout.MoveOutOfGroup(group.Id, ["app|b"]);

        Assert.Equal(["app|a", "app|b", "app|c", "app|d"], layout.Pages[0].Tiles.Select(t => t.Key));
    }

    [Fact]
    public void DissolveGroup_RestoresItemsInPlace()
    {
        var layout = new LauncherLayout();
        layout.Normalize(AppsAndFolders);
        var group = layout.CreateGroup(0, "常用", ["app|b", "app|c"], 1)!;

        layout.DissolveGroup(group.Id);

        Assert.Equal(["app|a", "app|b", "app|c", "app|d"], layout.Pages[0].Tiles.Select(t => t.Key));
    }

    [Fact]
    public void RenameGroup_UpdatesName()
    {
        var layout = new LauncherLayout();
        layout.Normalize(AppsAndFolders);
        var group = layout.CreateGroup(0, "旧名", ["app|a", "app|b"], 0)!;

        layout.RenameGroup(group.Id, "新名");

        Assert.Equal("新名", group.Name);
        Assert.Equal("新名", layout.FindGroup(group.Id)?.Name);
    }

    [Fact]
    public void JsonRoundTrip_PreservesPagesItemsAndGroups()
    {
        var layout = new LauncherLayout();
        layout.Normalize(AppsAndFolders);
        var group = layout.CreateGroup(0, "常用", ["app|a", "app|b"], 0)!;
        layout.MoveGroupItems(group.Id, ["app|b"], 0);

        var json = layout.ToJson();
        var restored = LauncherLayout.FromJson(JArray.Parse(json.ToString()));
        restored.Normalize(AppsAndFolders);

        Assert.Equal(3, restored.Pages.Count);
        Assert.Equal(["group|" + group.Id, "app|c", "app|d"],
            restored.Pages[0].Tiles.Select(LauncherLayout.TileId));
        Assert.Equal(["app|b", "app|a"], restored.FindGroup(group.Id)!.Items);
        Assert.Equal("常用", restored.FindGroup(group.Id)!.Name);
    }
}
