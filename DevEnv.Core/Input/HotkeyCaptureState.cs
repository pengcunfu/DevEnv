namespace DevEnv.Core.Input;

/// <summary>
/// 进程级快捷键录制状态。录制控件开始 / 结束录制时调用，宿主据此临时挂起
/// RegisterHotKey 与低级键盘钩子，避免录制时按到旧快捷键触发原有动作。
///
/// 使用引用计数：多个录制控件或异常路径嵌套调用也不会提前恢复。
/// </summary>
public static class HotkeyCaptureState
{
    private static readonly object Gate = new();
    private static int _count;

    /// <summary>是否正在录制快捷键；键盘钩子线程也会读取。</summary>
    public static bool IsCapturing
    {
        get
        {
            lock (Gate) return _count > 0;
        }
    }

    /// <summary>只在 false 与 true 切换时触发，参数表示当前是否处于录制状态。</summary>
    public static event Action<bool>? Changed;

    /// <summary>开始一次录制；Dispose 结束。必须在 UI 线程调用。</summary>
    public static IDisposable Begin()
    {
        bool changed;
        lock (Gate)
        {
            _count++;
            changed = _count == 1;
        }

        if (changed)
            Changed?.Invoke(true);
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            bool changed;
            lock (Gate)
            {
                if (_count == 0) return;
                _count--;
                changed = _count == 0;
            }

            if (changed)
                Changed?.Invoke(false);
        }
    }
}
