using CommunityToolkit.Mvvm.ComponentModel;
using DevEnv.Core.SigXor;
using DevEnv.Host.SigXor.Services;

namespace DevEnv.Host.SigXor.ViewModels;

public sealed partial class ArchiveEntryRow : ObservableObject
{
    public required string Path { get; init; }
    public required string KindText { get; init; }
    public required string SizeText { get; init; }
    public required string CompressedText { get; init; }
    public required string ModifiedText { get; init; }
}

/// <summary>压缩包目录预览。</summary>
public sealed partial class ArchiveContentViewModel : ObservableObject
{
    public ArchiveContentViewModel(string path, string name, long length)
    {
        FilePath = path;
        Name = name;
        Length = length;

        var listing = ArchiveListingService.List(path);
        Notice = listing.Notice;
        Error = listing.Notice is not null && listing.Entries.Count == 0 ? listing.Notice : null;

        var rows = listing.Entries.Select(e => new ArchiveEntryRow
        {
            Path = e.Path,
            KindText = e.IsDirectory ? "文件夹" : "文件",
            SizeText = ArchiveListingService.FormatEntrySize(e.Size),
            CompressedText = ArchiveListingService.FormatEntrySize(e.CompressedSize),
            ModifiedText = e.Modified?.ToString("yyyy-MM-dd HH:mm") ?? "",
        }).ToList();

        Entries = rows;
        Summary =
            $"{listing.FileCount:N0} 个文件 · {listing.DirectoryCount:N0} 个文件夹" +
            (listing.TotalUncompressed is { } t ? $" · 解压约 {FileEntry.FormatSize(t)}" : "") +
            (listing.Truncated ? " · 已截断" : "");
    }

    public string FilePath { get; }
    public string Name { get; }
    public long Length { get; }
    public string Summary { get; }
    public string? Notice { get; }
    public IReadOnlyList<ArchiveEntryRow> Entries { get; }

    [ObservableProperty]
    private string? _error;
}
