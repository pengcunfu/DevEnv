using System.Globalization;
using System.IO;
using DevEnv.Core.FileTools.Models;
using DevEnv.Core.Models;

namespace DevEnv.Core.FileTools.Services;

/// <summary>
/// 批量重命名：只处理指定文件夹顶层的文件（不递归、不含子文件夹）。
/// 先预览并校验非法字符、重名与已存在的目标文件，再分两步经临时名落盘，
/// 支持 A→B、B→C 这类链式改名；中途失败会尽量回滚。
/// </summary>
public sealed class BatchRenameService
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>按文件名排序返回文件夹顶层的文件；文件夹不存在时返回空列表。</summary>
    public IReadOnlyList<string> GetFiles(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return [];

        try
        {
            return Directory
                .EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public BatchRenamePreview Preview(string? folder, BatchRenameOptions options)
    {
        options ??= new BatchRenameOptions();
        var files = GetFiles(folder);
        var items = new List<BatchRenameItem>(files.Count);

        for (var i = 0; i < files.Count; i++)
        {
            var source = files[i];
            var original = Path.GetFileName(source);
            var newName = BuildName(original, i + 1, options);
            string? error = null;

            if (string.Equals(original, newName, StringComparison.Ordinal))
            {
                items.Add(new BatchRenameItem(source, original, newName, false, null));
                continue;
            }

            if (!ValidateName(newName, out error))
            {
                items.Add(new BatchRenameItem(source, original, newName, true, error));
                continue;
            }

            items.Add(new BatchRenameItem(source, original, newName, true, null));
        }

        MarkConflicts(folder, items);
        return BuildPreview(items);
    }

    /// <summary>执行预览中所有标记为「将重命名」且没有错误的条目。</summary>
    public OperationResult Apply(string? folder, IReadOnlyList<BatchRenameItem> items)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return OperationResult.Fail("文件夹不存在");

        var candidates = items
            .Where(item => item.Changed && !item.HasError)
            .ToList();
        if (candidates.Count == 0)
            return OperationResult.Fail("没有需要重命名的文件");

        // 落盘前按当前磁盘状态重新校验，避免预览后目录内容变化。
        var changingSources = new HashSet<string>(
            candidates.Select(item => item.SourcePath),
            StringComparer.OrdinalIgnoreCase);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in candidates)
        {
            var targetPath = Path.Combine(folder, item.NewName);
            if (!ValidateName(item.NewName, out var error))
                return OperationResult.Fail($"{item.OriginalName}：{error}");
            if (!targets.Add(item.NewName))
                return OperationResult.Fail($"目标文件名重复：{item.NewName}");
            if (File.Exists(targetPath)
                && !changingSources.Contains(targetPath)
                && !string.Equals(targetPath, item.SourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail($"目标文件已存在：{item.NewName}");
            }
        }

        var movedToTemp = new List<(string TempPath, string SourcePath)>();
        var completed = new List<(string SourcePath, string TargetPath)>();

        try
        {
            foreach (var item in candidates)
            {
                var tempPath = CreateTempPath(folder);
                File.Move(item.SourcePath, tempPath);
                movedToTemp.Add((tempPath, item.SourcePath));
            }

            foreach (var (item, index) in candidates.Select((item, index) => (item, index)))
            {
                var targetPath = Path.Combine(folder, item.NewName);
                File.Move(movedToTemp[index].TempPath, targetPath);
                completed.Add((item.SourcePath, targetPath));
            }
        }
        catch (Exception ex)
        {
            Rollback(movedToTemp, completed);
            return OperationResult.Fail($"重命名失败，已尽量回滚：{ex.Message}");
        }

        return OperationResult.Success($"已重命名 {completed.Count} 个文件");
    }

    private static void MarkConflicts(string? folder, List<BatchRenameItem> items)
    {
        var changing = items.Where(item => item.Changed && !item.HasError).ToList();
        var changingSources = new HashSet<string>(
            changing.Select(item => item.SourcePath),
            StringComparer.OrdinalIgnoreCase);

        var duplicates = changing
            .GroupBy(item => item.NewName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!item.Changed || item.HasError)
                continue;

            string? error = null;
            if (duplicates.Contains(item.NewName))
            {
                error = "与其他文件的新名称重复";
            }
            else if (!string.IsNullOrWhiteSpace(folder))
            {
                var targetPath = Path.Combine(folder, item.NewName);
                if (File.Exists(targetPath)
                    && !changingSources.Contains(targetPath)
                    && !string.Equals(targetPath, item.SourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    error = "目标文件已存在";
                }
            }

            if (error is not null)
                items[i] = item with { Error = error };
        }
    }

    private static BatchRenamePreview BuildPreview(IReadOnlyList<BatchRenameItem> items) =>
        new(
            items,
            items.Count(item => item.Changed && !item.HasError),
            items.Count(item => item.HasError));

    private static string BuildName(string original, int index, BatchRenameOptions options)
    {
        var extension = Path.GetExtension(original);
        var body = options.IncludeExtension
            ? original
            : Path.GetFileNameWithoutExtension(original);

        if (!string.IsNullOrEmpty(options.Find))
            body = body.Replace(options.Find, options.Replace ?? string.Empty, StringComparison.Ordinal);

        body = options.CaseMode switch
        {
            RenameCaseMode.Upper => body.ToUpperInvariant(),
            RenameCaseMode.Lower => body.ToLowerInvariant(),
            _ => body,
        };

        var name = (options.Prefix ?? string.Empty) + body + (options.Suffix ?? string.Empty);

        if (options.UseNumbering)
        {
            var step = options.Step == 0 ? 1 : options.Step;
            var number = options.StartNumber + (index - 1) * step;
            var width = Math.Clamp(options.NumberWidth, 1, 10);
            name += (options.NumberSeparator ?? string.Empty)
                + number.ToString($"D{width}", CultureInfo.InvariantCulture);
        }

        if (!options.IncludeExtension)
            name += extension;

        return name;
    }

    private static bool ValidateName(string name, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            error = "新文件名为空";
            return false;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = "新文件名包含非法字符";
            return false;
        }

        if (name.EndsWith(' ') || name.EndsWith('.'))
        {
            error = "文件名不能以空格或句点结尾";
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        if (ReservedNames.Contains(stem))
        {
            error = "新文件名是系统保留名称";
            return false;
        }

        return true;
    }

    private static string CreateTempPath(string folder)
    {
        while (true)
        {
            var path = Path.Combine(folder, $".devenv-rename-{Guid.NewGuid():N}.tmp");
            if (!File.Exists(path))
                return path;
        }
    }

    private static void Rollback(
        List<(string TempPath, string SourcePath)> movedToTemp,
        List<(string SourcePath, string TargetPath)> completed)
    {
        foreach (var (sourcePath, targetPath) in completed)
        {
            try
            {
                if (File.Exists(targetPath) && !File.Exists(sourcePath))
                    File.Move(targetPath, sourcePath);
            }
            catch
            {
                // 回滚尽力而为
            }
        }

        foreach (var (tempPath, sourcePath) in movedToTemp)
        {
            try
            {
                if (File.Exists(tempPath) && !File.Exists(sourcePath))
                    File.Move(tempPath, sourcePath);
            }
            catch
            {
                // 回滚尽力而为
            }
        }
    }
}
