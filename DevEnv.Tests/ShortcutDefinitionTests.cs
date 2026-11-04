using DevEnv.Core.SigXor.Toolkit;

namespace DevEnv.Tests.SigXor;

public class ShortcutDefinitionTests
{
    [Theory]
    [InlineData("right-alt", "right-alt")]
    [InlineData("LALT", "left-alt")]
    [InlineData("rightctrl", "right-ctrl")]
    [InlineData("caps", "caps-lock")]
    [InlineData("unknown", "right-alt")]
    [InlineData(null, "right-alt")]
    public void NormalizeVoiceShortcut_ReturnsCanonicalValue(string? input, string expected)
    {
        Assert.Equal(expected, ShortcutDefinition.NormalizeVoiceShortcut(input));
    }

    [Theory]
    [InlineData("left-alt", "左 Alt")]
    [InlineData("right-ctrl", "右 Ctrl")]
    [InlineData("left-ctrl", "左 Ctrl")]
    [InlineData("caps-lock", "Caps Lock")]
    [InlineData("right-alt", "右 Alt")]
    public void FormatVoiceShortcut_ReturnsDisplayName(string input, string expected)
    {
        Assert.Equal(expected, ShortcutDefinition.FormatVoiceShortcut(input));
    }

    [Theory]
    [InlineData("alt", "right-alt", "左 Alt + `")]
    [InlineData("alt", "left-alt", "右 Alt + `")]
    [InlineData("ctrl", "right-ctrl", "左 Ctrl + `")]
    [InlineData("ctrl", "left-ctrl", "右 Ctrl + `")]
    [InlineData("ctrl", "right-alt", "Ctrl + `")]
    [InlineData("ctrl+shift", "right-alt", "Ctrl + Shift + `")]
    [InlineData("win", "right-alt", "Win + `")]
    [InlineData("control", "right-alt", "Ctrl + `")]
    public void FormatCaptureModifier_AvoidsVoiceShortcut(
        string modifier,
        string voiceShortcut,
        string expected)
    {
        Assert.Equal(expected, ShortcutDefinition.FormatCaptureModifier(modifier, voiceShortcut));
    }

    [Fact]
    public void FormatCaptureModifier_BareBacktickWins()
    {
        Assert.Equal("`", ShortcutDefinition.FormatCaptureModifier("alt", "right-alt", allowBareBacktick: true));
    }

    [Theory]
    [InlineData("right-alt", 0xA5)]
    [InlineData("left-alt", 0xA4)]
    [InlineData("right-ctrl", 0xA3)]
    [InlineData("left-ctrl", 0xA2)]
    [InlineData("caps-lock", 0x14)]
    public void ToVoiceVirtualKey_ReturnsWindowsKeyCode(string shortcut, int expected)
    {
        Assert.Equal(expected, ShortcutDefinition.ToVoiceVirtualKey(shortcut));
    }
}
