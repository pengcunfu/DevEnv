using DevEnv.Core.Input;

namespace DevEnv.Host.SigXor.Services;

public sealed class GlobalHotkeyRouter
{
    private sealed class Registration : IDisposable
    {
        private readonly GlobalHotkeyRouter _owner;
        public int Priority { get; }
        public Func<int, bool>? KeyDown { get; }
        public Func<int, bool>? KeyUp { get; }

        public Registration(GlobalHotkeyRouter owner, int priority, Func<int, bool>? keyDown, Func<int, bool>? keyUp)
        {
            _owner = owner;
            Priority = priority;
            KeyDown = keyDown;
            KeyUp = keyUp;
        }

        public void Dispose() => _owner.Remove(this);
    }

    private readonly List<Registration> _handlers = [];
    private readonly object _lock = new();

    public IDisposable Register(int priority, Func<int, bool>? keyDown, Func<int, bool>? keyUp = null)
    {
        if (keyDown is null && keyUp is null)
            throw new ArgumentException("至少需要一个按键回调。", nameof(keyDown));

        var registration = new Registration(this, priority, keyDown, keyUp);
        lock (_lock)
        {
            _handlers.Add(registration);
            _handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        return registration;
    }

    private void Remove(Registration registration)
    {
        lock (_lock) _handlers.Remove(registration);
    }

    private Registration[] Snapshot()
    {
        lock (_lock) return [.. _handlers];
    }

    public bool HandleKeyDown(int vkCode)
    {
        // 设置页录制快捷键期间，让按键直接到达录制控件，不触发截图 / 语音 / 预览
        if (HotkeyCaptureState.IsCapturing) return false;

        foreach (Registration handler in Snapshot())
        {
            if (handler.KeyDown?.Invoke(vkCode) == true)
                return true;
        }
        return false;
    }

    public bool HandleKeyUp(int vkCode)
    {
        if (HotkeyCaptureState.IsCapturing) return false;

        bool handled = false;
        foreach (Registration handler in Snapshot())
        {
            if (handler.KeyUp?.Invoke(vkCode) == true)
                handled = true;
        }
        return handled;
    }
}
