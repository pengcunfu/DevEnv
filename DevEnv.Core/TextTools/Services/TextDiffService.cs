using DevEnv.Core.TextTools.Models;

namespace DevEnv.Core.TextTools.Services;

/// <summary>
/// 文本逐行对比：用最长公共子序列生成「相同 / 新增 / 删除」行，
/// 可忽略大小写或空白差异。单侧最多 3000 行，避免大文本把内存打满。
/// </summary>
public sealed class TextDiffService
{
    public const int MaxLinesPerSide = 3000;

    public TextDiffResult Compare(
        string? leftText,
        string? rightText,
        bool ignoreCase = false,
        bool ignoreWhitespace = false)
    {
        var left = SplitLines(leftText);
        var right = SplitLines(rightText);

        if (left.Length > MaxLinesPerSide || right.Length > MaxLinesPerSide)
        {
            return TextDiffResult.Fail(
                $"单侧最多对比 {MaxLinesPerSide} 行（当前 {left.Length} / {right.Length} 行）");
        }

        var leftKeys = left.Select(line => Normalize(line, ignoreCase, ignoreWhitespace)).ToArray();
        var rightKeys = right.Select(line => Normalize(line, ignoreCase, ignoreWhitespace)).ToArray();

        // 从后向前填 LCS 长度表，再从前往后回溯生成差异行。
        var table = new int[left.Length + 1, right.Length + 1];
        for (var i = left.Length - 1; i >= 0; i--)
        {
            for (var j = right.Length - 1; j >= 0; j--)
            {
                table[i, j] = leftKeys[i] == rightKeys[j]
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        var lines = new List<DiffLine>(left.Length + right.Length);
        int a = 0, b = 0, oldLine = 0, newLine = 0, added = 0, removed = 0;

        while (a < left.Length && b < right.Length)
        {
            if (leftKeys[a] == rightKeys[b])
            {
                lines.Add(new DiffLine(DiffLineKind.Same, ++oldLine, ++newLine, left[a]));
                a++;
                b++;
            }
            else if (table[a + 1, b] >= table[a, b + 1])
            {
                lines.Add(new DiffLine(DiffLineKind.Removed, ++oldLine, 0, left[a]));
                removed++;
                a++;
            }
            else
            {
                lines.Add(new DiffLine(DiffLineKind.Added, 0, ++newLine, right[b]));
                added++;
                b++;
            }
        }

        while (a < left.Length)
        {
            lines.Add(new DiffLine(DiffLineKind.Removed, ++oldLine, 0, left[a++]));
            removed++;
        }

        while (b < right.Length)
        {
            lines.Add(new DiffLine(DiffLineKind.Added, 0, ++newLine, right[b++]));
            added++;
        }

        return new TextDiffResult(true, string.Empty, lines, added, removed);
    }

    private static string[] SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');

        // 末尾换行不额外算一行，避免两侧都多出一条空行。
        if (lines.Length > 0 && lines[^1].Length == 0)
            lines = lines[..^1];

        return lines;
    }

    private static string Normalize(string line, bool ignoreCase, bool ignoreWhitespace)
    {
        if (ignoreWhitespace)
            line = string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (ignoreCase)
            line = line.ToUpperInvariant();

        return line;
    }
}
