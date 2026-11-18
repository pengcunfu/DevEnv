using DevEnv.Core.Services;

namespace DevEnv.Tests;

/// <summary>启动器文件夹与资源管理器快速访问的对账规则（不碰真实 Shell，只测纯逻辑）。</summary>
public sealed class QuickAccessSyncTests
{
    [Fact]
    public void ImportsQuickAccessFoldersMissingFromLauncher()
    {
        var plan = QuickAccessReconciler.Build(
            [(@"C:\Data\Projects", true)],
            [@"C:\Data\Projects", @"C:\Data\Downloads"]);

        Assert.Equal(new[] { @"C:\Data\Downloads" }, plan.ImportPaths);
        Assert.Empty(plan.RemoveIndexes);
        Assert.Empty(plan.MarkPinnedIndexes);
    }

    [Fact]
    public void MarksExistingLauncherFoldersThatArePinned()
    {
        var plan = QuickAccessReconciler.Build(
            [(@"C:\Data\Projects", false), (@"C:\Data\Manual", false)],
            [@"c:\data\projects\"]);

        Assert.Empty(plan.ImportPaths);
        Assert.Equal(new[] { 0 }, plan.MarkPinnedIndexes);
        Assert.Empty(plan.RemoveIndexes);
    }

    [Fact]
    public void RemovesPinnedFoldersNoLongerInQuickAccess()
    {
        var plan = QuickAccessReconciler.Build(
            [(@"C:\Data\Pinned", true), (@"C:\Data\Gone", true)],
            [@"C:\Data\Pinned"]);

        Assert.Empty(plan.ImportPaths);
        Assert.Equal(new[] { 1 }, plan.RemoveIndexes);
        Assert.Empty(plan.MarkPinnedIndexes);
    }

    [Fact]
    public void KeepsManualFoldersThatWereNeverPinned()
    {
        var plan = QuickAccessReconciler.Build(
            [(@"C:\Data\Manual", false)],
            []);

        Assert.Empty(plan.ImportPaths);
        Assert.Empty(plan.RemoveIndexes);
        Assert.Empty(plan.MarkPinnedIndexes);
    }

    [Fact]
    public void DeduplicatesQuickAccessEntriesByNormalizedPath()
    {
        var plan = QuickAccessReconciler.Build(
            [],
            [@"C:\Data\Projects", @"c:\data\projects\", @"C:\Data\Downloads\"]);

        Assert.Equal(new[] { @"C:\Data\Projects", @"C:\Data\Downloads" }, plan.ImportPaths);
    }

    [Fact]
    public void IgnoresEmptyPaths()
    {
        var plan = QuickAccessReconciler.Build(
            [(string.Empty, true), (@"C:\Data\Kept", true)],
            ["", "   ", @"C:\Data\Kept"]);

        Assert.Empty(plan.ImportPaths);
        Assert.Empty(plan.RemoveIndexes);
        Assert.Empty(plan.MarkPinnedIndexes);
    }
}
