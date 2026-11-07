using DevEnv.Core.SigXor;

namespace DevEnv.Tests.SigXor;

public sealed class PreviewHistoryTests
{
    private static FileEntry Entry(string path) => new()
    {
        Name = Path.GetFileName(path),
        FullPath = path,
    };

    [Fact]
    public void Reset_Starts_At_First_File()
    {
        var history = new PreviewHistory();
        history.Reset([Entry(@"C:\a.txt"), Entry(@"C:\b.txt")]);

        Assert.Equal(2, history.Count);
        Assert.Equal(@"C:\a.txt", history.Current?.FullPath);
        Assert.False(history.CanGoPrevious);
        Assert.True(history.CanGoNext);
        Assert.Equal("1 / 2", history.PositionText);
    }

    [Fact]
    public void Append_Keeps_Earlier_Files_And_Selects_New_Batch()
    {
        var history = new PreviewHistory();
        history.Reset([Entry(@"C:\a.txt")]);

        history.Append([Entry(@"C:\b.txt"), Entry(@"C:\c.txt")]);

        Assert.Equal(3, history.Count);
        Assert.Equal(@"C:\b.txt", history.Current?.FullPath);
        Assert.True(history.CanGoPrevious);
        Assert.True(history.CanGoNext);
        Assert.Equal("2 / 3", history.PositionText);
    }

    [Fact]
    public void MovePrevious_And_Next_Respect_Boundaries()
    {
        var history = new PreviewHistory();
        history.Reset([Entry(@"C:\a.txt"), Entry(@"C:\b.txt"), Entry(@"C:\c.txt")]);

        Assert.True(history.MoveNext());
        Assert.Equal(@"C:\b.txt", history.Current?.FullPath);
        Assert.True(history.MoveNext());
        Assert.Equal(@"C:\c.txt", history.Current?.FullPath);
        Assert.False(history.MoveNext());

        Assert.True(history.MovePrevious());
        Assert.True(history.MovePrevious());
        Assert.Equal(@"C:\a.txt", history.Current?.FullPath);
        Assert.False(history.MovePrevious());
    }

    [Fact]
    public void Append_Deduplicates_Same_Path()
    {
        var history = new PreviewHistory();
        history.Reset([Entry(@"C:\a.txt"), Entry(@"C:\b.txt")]);

        history.Append([Entry(@"C:\a.txt")]);

        Assert.Equal(2, history.Count);
        Assert.Equal(@"C:\a.txt", history.Current?.FullPath);
        Assert.Equal("2 / 2", history.PositionText);
    }

    [Fact]
    public void Empty_History_Has_No_Current()
    {
        var history = new PreviewHistory();
        history.Reset([]);

        Assert.Equal(0, history.Count);
        Assert.Null(history.Current);
        Assert.False(history.CanGoPrevious);
        Assert.False(history.CanGoNext);
        Assert.Equal("", history.PositionText);
    }
}
