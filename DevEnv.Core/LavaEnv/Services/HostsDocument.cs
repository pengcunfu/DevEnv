using System.Net;
using System.Text.RegularExpressions;
using DevEnv.Core.LavaEnv.Models;

namespace DevEnv.Core.LavaEnv.Services;

/// <summary>
/// Hosts 文本 ↔ 条目列表互转。按条编辑时用它把改动写回原文，尽量保留注释与空行。
/// </summary>
public static class HostsDocument
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>从全文解析可编辑条目（跳过注释与空行）。</summary>
    public static IReadOnlyList<HostsEntry> ParseEntries(string? text)
    {
        var lines = NormalizeLines(text);
        var entries = new List<HostsEntry>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!HostsEntry.TryParse(lines[i], out var entry) || entry is null)
                continue;

            entry.LineNumber = i + 1;
            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// 把条目列表写回原文：保留非条目行；按原行号替换或删除条目；
    /// <see cref="HostsEntry.LineNumber"/> ≤ 0 的视为新增，追加到末尾。
    /// </summary>
    public static string ApplyEntries(string? originalText, IReadOnlyList<HostsEntry> entries)
    {
        var text = originalText ?? string.Empty;
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = NormalizeLines(text).ToList();

        var originalEntryLines = new HashSet<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (HostsEntry.TryParse(lines[i], out _))
                originalEntryLines.Add(i + 1);
        }

        var byLine = new Dictionary<int, HostsEntry>();
        foreach (var entry in entries)
        {
            if (entry.LineNumber > 0)
                byLine[entry.LineNumber] = entry;
        }

        var result = new List<string>(lines.Count + 4);
        for (var i = 0; i < lines.Count; i++)
        {
            var lineNumber = i + 1;
            if (!originalEntryLines.Contains(lineNumber))
            {
                result.Add(lines[i]);
                continue;
            }

            if (byLine.TryGetValue(lineNumber, out var entry))
                result.Add(FormatLine(entry));
            // 已删除的原条目行：跳过
        }

        foreach (var entry in entries.Where(e => e.LineNumber <= 0))
        {
            var line = FormatLine(entry);
            if (!string.IsNullOrWhiteSpace(line))
                result.Add(line);
        }

        // 原文末尾若有换行，保持；否则按行拼接
        var body = string.Join(newline, result);
        if (text.EndsWith("\n", StringComparison.Ordinal) || text.EndsWith("\r", StringComparison.Ordinal))
            return body + newline;
        return body;
    }

    public static string FormatLine(HostsEntry entry)
    {
        var ip = entry.Ip?.Trim() ?? string.Empty;
        var domain = Whitespace.Replace(entry.Domain?.Trim() ?? string.Empty, " ").Trim();
        if (ip.Length == 0 && domain.Length == 0)
            return string.Empty;
        return $"{ip}\t{domain}";
    }

    /// <summary>判断一行是否为可解析的条目（供 UI / 测试使用）。</summary>
    public static bool IsEntryLine(string? line) => HostsEntry.TryParse(line ?? string.Empty, out _);

    /// <summary>地址是否合法（IPv4 四段十进制 / IPv6）。</summary>
    public static bool IsValidAddress(string? value) => HostsEntry.TryParseAddress(value, out _);

    private static string[] NormalizeLines(string? text) =>
        (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
}
