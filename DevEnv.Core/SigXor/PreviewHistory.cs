namespace DevEnv.Core.SigXor;

/// <summary>
/// 预览窗口的浏览历史：保留本次打开期间预览过的文件，支持前后切换。
/// 同一路径只保留一条记录，再次预览时移到末尾，避免来回切换产生重复项。
/// </summary>
public sealed class PreviewHistory
{
    private readonly List<FileEntry> _items = [];
    private int _index = -1;

    public IReadOnlyList<FileEntry> Items => _items;

    public int Count => _items.Count;

    public int Index => _index;

    public FileEntry? Current => _index >= 0 && _index < _items.Count ? _items[_index] : null;

    public bool CanGoPrevious => _index > 0;

    public bool CanGoNext => _index >= 0 && _index < _items.Count - 1;

    /// <summary>文件位置文案，例如「2 / 5」；只有一个文件时为空。</summary>
    public string PositionText => _items.Count <= 1 ? string.Empty : $"{_index + 1} / {_items.Count}";

    /// <summary>重置历史（预览窗口重新打开时调用），当前项为第一个文件。</summary>
    public void Reset(IEnumerable<FileEntry> files)
    {
        _items.Clear();
        _items.AddRange(files);
        _index = _items.Count > 0 ? 0 : -1;
    }

    /// <summary>追加一批文件（窗口打开期间又预览了新文件），当前项切到这批的第一个。</summary>
    public void Append(IEnumerable<FileEntry> files)
    {
        var batch = files.ToList();
        if (batch.Count == 0) return;
        if (_items.Count == 0)
        {
            Reset(batch);
            return;
        }

        foreach (var file in batch)
        {
            _items.RemoveAll(x => string.Equals(x.FullPath, file.FullPath, StringComparison.OrdinalIgnoreCase));
        }

        _index = _items.Count;
        _items.AddRange(batch);
    }

    public bool MovePrevious()
    {
        if (!CanGoPrevious) return false;
        _index--;
        return true;
    }

    public bool MoveNext()
    {
        if (!CanGoNext) return false;
        _index++;
        return true;
    }
}
