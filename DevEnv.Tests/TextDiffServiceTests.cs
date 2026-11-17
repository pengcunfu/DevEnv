using DevEnv.Core.TextTools.Models;
using DevEnv.Core.TextTools.Services;

namespace DevEnv.Tests;

public sealed class TextDiffServiceTests
{
    private readonly TextDiffService _service = new();

    [Fact]
    public void Compare_IdenticalTextIsAllSame()
    {
        var result = _service.Compare("a\nb\nc", "a\nb\nc");

        Assert.True(result.Ok);
        Assert.Equal(3, result.Lines.Count);
        Assert.All(result.Lines, line => Assert.Equal(DiffLineKind.Same, line.Kind));
        Assert.Equal(0, result.AddedCount);
        Assert.Equal(0, result.RemovedCount);
    }

    [Fact]
    public void Compare_DetectsAddedAndRemovedLines()
    {
        var result = _service.Compare("a\nb\nc", "a\nx\nc");

        Assert.True(result.Ok);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.RemovedCount);

        var removed = Assert.Single(result.Lines, line => line.Kind == DiffLineKind.Removed);
        Assert.Equal("b", removed.Text);
        Assert.Equal(2, removed.OldLineNumber);
        Assert.Equal(0, removed.NewLineNumber);

        var added = Assert.Single(result.Lines, line => line.Kind == DiffLineKind.Added);
        Assert.Equal("x", added.Text);
        Assert.Equal(0, added.OldLineNumber);
        Assert.Equal(2, added.NewLineNumber);
    }

    [Fact]
    public void Compare_LineNumbersStayCorrectWithInsertions()
    {
        var result = _service.Compare("a\nc", "a\nb\nc");

        var sameC = result.Lines.Last();
        Assert.Equal(DiffLineKind.Same, sameC.Kind);
        Assert.Equal(2, sameC.OldLineNumber);
        Assert.Equal(3, sameC.NewLineNumber);
    }

    [Fact]
    public void Compare_HandlesCrLfAndTrailingNewline()
    {
        var result = _service.Compare("a\r\nb\r\n", "a\nb");

        Assert.True(result.Ok);
        Assert.All(result.Lines, line => Assert.Equal(DiffLineKind.Same, line.Kind));
    }

    [Fact]
    public void Compare_IgnoreWhitespaceCollapsesSpaces()
    {
        var strict = _service.Compare("a  b", "a b");
        var loose = _service.Compare("a  b", "a b", ignoreWhitespace: true);

        Assert.Equal(1, strict.AddedCount);
        Assert.Equal(1, strict.RemovedCount);
        Assert.Equal(0, loose.AddedCount);
        Assert.Equal(0, loose.RemovedCount);
    }

    [Fact]
    public void Compare_IgnoreCaseWorks()
    {
        var strict = _service.Compare("FireNeb", "fireneb");
        var loose = _service.Compare("FireNeb", "fireneb", ignoreCase: true);

        Assert.Equal(1, strict.AddedCount);
        Assert.Equal(0, loose.AddedCount);
    }

    [Fact]
    public void Compare_EmptyTextsProduceNoLines()
    {
        var result = _service.Compare(string.Empty, string.Empty);

        Assert.True(result.Ok);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Compare_OverLineLimitReportsError()
    {
        var text = string.Join('\n', Enumerable.Range(0, TextDiffService.MaxLinesPerSide + 1));
        var result = _service.Compare(text, "a");

        Assert.False(result.Ok);
        Assert.Contains("最多", result.Error, StringComparison.Ordinal);
    }
}
