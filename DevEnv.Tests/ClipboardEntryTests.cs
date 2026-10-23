using DevEnv.Core.Clipboard.Models;

namespace DevEnv.Tests;

public sealed class ClipboardEntryTests
{
    [Fact]
    public void IsImage_CaseInsensitive()
    {
        Assert.True(new ClipboardEntry { Type = "image" }.IsImage);
        Assert.True(new ClipboardEntry { Type = "IMAGE" }.IsImage);
        Assert.False(new ClipboardEntry { Type = "text" }.IsImage);
        Assert.False(new ClipboardEntry().IsImage);
    }

    [Fact]
    public void DisplayText_TextEntry_CollapsesWhitespace()
    {
        var entry = new ClipboardEntry { Timestamp = "2026-10-23 10:00:00", Text = "  hello \t world \r\n again " };

        Assert.Equal("2026-10-23 10:00:00\nhello world again", entry.DisplayText);
    }

    [Fact]
    public void DisplayText_LongText_IsTruncatedWithEllipsis()
    {
        var entry = new ClipboardEntry { Timestamp = "T", Text = new string('x', 400) };
        var preview = entry.DisplayText[(entry.DisplayText.IndexOf('\n') + 1)..];

        Assert.Equal(303, preview.Length);
        Assert.EndsWith("...", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayText_ImageEntry_UsesFileName()
    {
        var entry = new ClipboardEntry { Type = "image", Timestamp = "T", ImagePath = @"D:\pics\shot.png" };

        Assert.Equal("T\n[图片] shot.png", entry.DisplayText);
    }

    [Fact]
    public void DisplayText_ImageWithoutPath_FallsBackToUnknown()
    {
        var entry = new ClipboardEntry { Type = "image", Timestamp = "T" };

        Assert.Equal("T\n[图片] unknown.png", entry.DisplayText);
    }
}