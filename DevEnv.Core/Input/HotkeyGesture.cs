namespace DevEnv.Core.Input;

/// <summary>组合键修饰键，数值与 Win32 的 MOD_* 一致，可直接用于 RegisterHotKey。</summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
}

/// <summary>一个组合键：修饰键 + 虚拟键。纯数据，不依赖 WPF/Win32。</summary>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    public bool IsEmpty => VirtualKey == 0;

    public override string ToString() => HotkeyParser.FormatCanonical(this);
}
