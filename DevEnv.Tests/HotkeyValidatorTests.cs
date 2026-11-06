using DevEnv.Core.Input;

namespace DevEnv.Tests;

public class HotkeyValidatorTests
{
    [Theory]
    [InlineData("Ctrl+Alt+D", true)]
    [InlineData("Alt+Space", true)]
    [InlineData("Ctrl+Shift+`", true)]
    [InlineData("Shift+D", false)]
    [InlineData("D", false)]
    [InlineData("Alt+Tab", false)]
    [InlineData("Alt+F4", false)]
    [InlineData("Ctrl+Esc", false)]
    [InlineData("Ctrl+Alt+Delete", false)]
    [InlineData("Win+L", false)]
    public void Validate_ChecksModifiersAndReservedKeys(string text, bool expected)
    {
        Assert.True(HotkeyParser.TryParse(text, out HotkeyGesture gesture));
        Assert.Equal(expected, HotkeyValidator.Validate(gesture).IsValid);
    }

    [Fact]
    public void ValidateBindings_ReportsDuplicate()
    {
        var errors = HotkeyValidator.ValidateBindings(
        [
            ("显示主窗口", "Ctrl+Alt+D"),
            ("快捷应用", "Ctrl+Alt+D"),
        ]);

        Assert.Contains(errors, e => e.Id == "快捷应用");
    }

    [Fact]
    public void ValidateBindings_AcceptsDifferentKeys()
    {
        var errors = HotkeyValidator.ValidateBindings(
        [
            ("显示主窗口", "Ctrl+Alt+D"),
            ("快捷应用", "Alt+Space"),
        ]);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateBindings_ReportsMalformedText()
    {
        var errors = HotkeyValidator.ValidateBindings(
        [
            ("显示主窗口", "Ctrl+Unknown"),
        ]);

        Assert.Single(errors);
        Assert.Equal("显示主窗口", errors[0].Id);
    }
}
