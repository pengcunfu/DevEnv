using DevEnv.Core.Input;

namespace DevEnv.Tests;

public class HotkeyParserTests
{
    [Theory]
    [InlineData("Ctrl+Alt+D", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44)]
    [InlineData("ctrl + alt + d", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44)]
    [InlineData("Control+Alt+D", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44)]
    [InlineData("Alt+Space", HotkeyModifiers.Alt, 0x20)]
    [InlineData("Ctrl+Shift+`", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0xC0)]
    [InlineData("Ctrl+Shift+F12", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x7B)]
    public void TryParse_ReturnsGesture(string text, HotkeyModifiers modifiers, int virtualKey)
    {
        Assert.True(HotkeyParser.TryParse(text, out HotkeyGesture gesture));
        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(virtualKey, gesture.VirtualKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+Unknown")]
    [InlineData("Ctrl+A+B")]
    public void TryParse_RejectsInvalidText(string? text)
    {
        Assert.False(HotkeyParser.TryParse(text, out _));
    }

    [Theory]
    [InlineData("Ctrl+Alt+D", "Ctrl+Alt+D")]
    [InlineData("Alt+Space", "Alt+Space")]
    [InlineData("Ctrl+Shift+`", "Ctrl+Shift+`")]
    [InlineData("Win+Left", "Win+Left")]
    [InlineData("Ctrl+Num7", "Ctrl+Num7")]
    public void FormatCanonical_RoundTrips(string text, string expected)
    {
        Assert.True(HotkeyParser.TryParse(text, out HotkeyGesture gesture));
        Assert.Equal(expected, HotkeyParser.FormatCanonical(gesture));
    }

    [Theory]
    [InlineData("Space", 0x20)]
    [InlineData("esc", 0x1B)]
    [InlineData("Return", 0x0D)]
    [InlineData("PageUp", 0x21)]
    [InlineData("Down", 0x28)]
    [InlineData("F24", 0x87)]
    [InlineData("`", 0xC0)]
    [InlineData("=", 0xBB)]
    [InlineData("Num7", 0x67)]
    public void TryParseKey_ReturnsVirtualKey(string token, int expected)
    {
        Assert.True(HotkeyParser.TryParseKey(token, out int virtualKey));
        Assert.Equal(expected, virtualKey);
    }

    [Fact]
    public void IsModifierKey_DistinguishesModifiers()
    {
        Assert.True(HotkeyParser.IsModifierKey(0xA4));
        Assert.True(HotkeyParser.IsModifierKey(0x5B));
        Assert.False(HotkeyParser.IsModifierKey(0x44));
    }
}
