namespace DevEnv.Core.SigXor.Csv;

/// <summary>轻量 CSV/TSV 解析：支持引号字段与转义，供表格预览使用。</summary>
public static class CsvTableParser
{
    public const int MaxPreviewRows = 10_000;
    public const int MaxPreviewColumns = 256;

    public sealed record Result(
        IReadOnlyList<string> Headers,
        IReadOnlyList<IReadOnlyList<string>> Rows,
        bool TruncatedRows,
        bool TruncatedColumns,
        char Delimiter);

    public static Result Parse(string text, char? delimiter = null)
    {
        char sep = delimiter ?? DetectDelimiter(text);
        var rows = new List<List<string>>();
        bool truncatedRows = false;
        bool truncatedColumns = false;

        using var reader = new StringReader(text);
        string? line;
        while ((line = ReadLogicalLine(reader)) is not null)
        {
            if (rows.Count >= MaxPreviewRows)
            {
                truncatedRows = true;
                break;
            }

            var fields = SplitLine(line, sep);
            if (fields.Count > MaxPreviewColumns)
            {
                truncatedColumns = true;
                fields = fields.Take(MaxPreviewColumns).ToList();
            }
            rows.Add(fields);
        }

        if (rows.Count == 0)
            return new Result([], [], false, false, sep);

        int width = rows.Max(r => r.Count);
        foreach (var row in rows)
        {
            while (row.Count < width) row.Add("");
        }

        // 首行作表头；若看起来像数据也仍用作表头（预览约定）
        var headers = rows[0];
        var data = rows.Skip(1).Cast<IReadOnlyList<string>>().ToList();
        return new Result(headers, data, truncatedRows, truncatedColumns, sep);
    }

    public static char DetectDelimiter(string text)
    {
        string sample = text.Length <= 4096 ? text : text[..4096];
        int commas = sample.Count(c => c == ',');
        int tabs = sample.Count(c => c == '\t');
        int semis = sample.Count(c => c == ';');
        if (tabs >= commas && tabs >= semis && tabs > 0) return '\t';
        if (semis > commas && semis > 0) return ';';
        return ',';
    }

    /// <summary>读取一行，尊重引号内换行。</summary>
    private static string? ReadLogicalLine(TextReader reader)
    {
        int first = reader.Read();
        if (first < 0) return null;

        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;
        int ch = first;
        while (ch >= 0)
        {
            char c = (char)ch;
            if (c == '"')
            {
                inQuotes = !inQuotes;
                sb.Append(c);
            }
            else if (!inQuotes && (c == '\n' || c == '\r'))
            {
                if (c == '\r' && reader.Peek() == '\n') reader.Read();
                break;
            }
            else
            {
                sb.Append(c);
            }
            ch = reader.Read();
        }
        return sb.ToString();
    }

    private static List<string> SplitLine(string line, char sep)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == sep)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields;
    }
}
