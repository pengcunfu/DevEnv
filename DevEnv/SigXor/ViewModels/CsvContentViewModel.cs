using System.Data;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using DevEnv.Core.SigXor;
using DevEnv.Core.SigXor.Csv;

namespace DevEnv.Host.SigXor.ViewModels;

/// <summary>CSV/TSV 表格预览。</summary>
public sealed partial class CsvContentViewModel : ObservableObject
{
    public CsvContentViewModel(string path, string name, string text, char? delimiter = null)
    {
        FilePath = path;
        Name = name;
        var parsed = CsvTableParser.Parse(text, delimiter ?? DetectFromPath(path));
        DelimiterName = parsed.Delimiter switch
        {
            '\t' => "Tab",
            ';' => "分号",
            _ => "逗号",
        };

        var table = new DataTable();
        for (int i = 0; i < parsed.Headers.Count; i++)
        {
            string header = string.IsNullOrWhiteSpace(parsed.Headers[i])
                ? $"列{i + 1}"
                : parsed.Headers[i];
            // DataTable 列名需唯一
            string unique = header;
            int n = 2;
            while (table.Columns.Contains(unique))
                unique = $"{header}_{n++}";
            table.Columns.Add(unique);
        }

        foreach (var row in parsed.Rows)
        {
            var dr = table.NewRow();
            for (int i = 0; i < table.Columns.Count && i < row.Count; i++)
                dr[i] = row[i];
            table.Rows.Add(dr);
        }

        Table = table;
        RowCount = parsed.Rows.Count;
        ColumnCount = parsed.Headers.Count;

        var notes = new List<string>();
        if (parsed.TruncatedRows) notes.Add($"行数超过 {CsvTableParser.MaxPreviewRows:N0}，已截断");
        if (parsed.TruncatedColumns) notes.Add($"列数超过 {CsvTableParser.MaxPreviewColumns}，已截断");
        Notice = notes.Count == 0 ? null : string.Join(" · ", notes);
        Summary = $"{RowCount:N0} 行 × {ColumnCount} 列 · 分隔符 {DelimiterName}" +
                  (Notice is null ? "" : $" · {Notice}");
    }

    public string FilePath { get; }
    public string Name { get; }
    public DataTable Table { get; }
    public int RowCount { get; }
    public int ColumnCount { get; }
    public string DelimiterName { get; }
    public string Summary { get; }
    public string? Notice { get; }

    private static char DetectFromPath(string path) =>
        Path.GetExtension(path).Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',';
}
