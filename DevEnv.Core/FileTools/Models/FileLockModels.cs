namespace DevEnv.Core.FileTools.Models;

/// <summary>Restart Manager 检测到的占用进程。</summary>
public sealed class LockedProcessInfo
{
    public int ProcessId { get; init; }

    /// <summary>Restart Manager 给出的应用名；拿不到时回退到进程名。</summary>
    public string Name { get; init; } = string.Empty;

    public string ExecutablePath { get; init; } = string.Empty;

    /// <summary>应用类型（窗口程序 / 服务 / 关键系统进程等）。</summary>
    public string AppTypeText { get; init; } = string.Empty;

    public bool IsService { get; init; }

    /// <summary>Restart Manager 标记的关键系统进程：界面上禁止结束。</summary>
    public bool IsCritical { get; init; }

    public bool IsCurrentProcess { get; init; }

    public string StartTimeText { get; init; } = string.Empty;

    /// <summary>界面是否允许对该进程执行「结束进程」。</summary>
    public bool CanEnd => !IsCritical && !IsCurrentProcess;

    /// <summary>进程详情行：路径或「无法读取进程路径」。</summary>
    public string PathText => string.IsNullOrWhiteSpace(ExecutablePath) ? "—" : ExecutablePath;
}

/// <summary>一次占用扫描的结果。</summary>
public sealed class FileLockScanResult
{
    public IReadOnlyList<LockedProcessInfo> Processes { get; init; } = [];

    /// <summary>枚举文件夹或读取进程信息时的非致命提示。</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>实际提交给 Restart Manager 的文件数（文件夹会被展开）。</summary>
    public int RegisteredFileCount { get; init; }

    /// <summary>文件夹内文件过多，只检测了前一部分。</summary>
    public bool Truncated { get; init; }
}
