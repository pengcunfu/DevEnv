using DevEnv.Core.Input;

namespace DevEnv.Tests;

public sealed class WinKeyGestureTrackerTests
{
    private const int VkE = 0x45;
    private const int VkLeftWin = WinKeyGestureTracker.VkLeftWin;
    private const int VkRightWin = WinKeyGestureTracker.VkRightWin;

    [Fact]
    public void LoneWinPress_TriggersAndSwallowsKeyUp()
    {
        var tracker = new WinKeyGestureTracker();

        var down = tracker.OnKeyDown(VkLeftWin);
        var up = tracker.OnKeyUp(VkLeftWin);

        Assert.False(down.Swallow);
        Assert.False(down.Trigger);
        Assert.True(up.Swallow);
        Assert.True(up.Trigger);
        Assert.False(tracker.IsWinHeld);
    }

    [Fact]
    public void RightWin_AlsoTriggers()
    {
        var tracker = new WinKeyGestureTracker();

        tracker.OnKeyDown(VkRightWin);
        var up = tracker.OnKeyUp(VkRightWin);

        Assert.True(up.Trigger);
    }

    [Fact]
    public void WinPlusOtherKey_DoesNotTriggerAndPassesThrough()
    {
        var tracker = new WinKeyGestureTracker();

        tracker.OnKeyDown(VkLeftWin);
        tracker.OnKeyDown(VkE);
        var up = tracker.OnKeyUp(VkLeftWin);

        // Win+E：抬起必须放行，否则每次组合键都会把 Win 键「卡住」
        Assert.False(up.Swallow);
        Assert.False(up.Trigger);
    }

    [Fact]
    public void OtherKeyReleasedBeforeWin_StillCountsAsCombo()
    {
        var tracker = new WinKeyGestureTracker();

        tracker.OnKeyDown(VkLeftWin);
        tracker.OnKeyDown(VkE);
        tracker.OnKeyUp(VkE);
        var up = tracker.OnKeyUp(VkLeftWin);

        Assert.False(up.Trigger);
    }

    [Fact]
    public void WinAutoRepeat_StillCountsAsLonePress()
    {
        var tracker = new WinKeyGestureTracker();

        tracker.OnKeyDown(VkLeftWin);
        // 按住不放时系统会持续送重复的按下事件
        tracker.OnKeyDown(VkLeftWin);
        tracker.OnKeyDown(VkLeftWin);
        var up = tracker.OnKeyUp(VkLeftWin);

        Assert.True(up.Trigger);
    }

    [Fact]
    public void BothWinKeys_CountAsCombo()
    {
        var tracker = new WinKeyGestureTracker();

        tracker.OnKeyDown(VkLeftWin);
        tracker.OnKeyDown(VkRightWin);
        var up = tracker.OnKeyUp(VkLeftWin);

        Assert.False(up.Trigger);
        Assert.False(up.Swallow);
    }

    [Fact]
    public void KeyUpWithoutWinDown_IsIgnored()
    {
        var tracker = new WinKeyGestureTracker();

        var result = tracker.OnKeyUp(VkE);

        Assert.False(result.Swallow);
        Assert.False(result.Trigger);
    }

    [Fact]
    public void PlainKey_DoesNotEnterWinGesture()
    {
        var tracker = new WinKeyGestureTracker();

        tracker.OnKeyDown(VkE);
        var up = tracker.OnKeyUp(VkE);

        Assert.False(tracker.IsWinHeld);
        Assert.False(up.Trigger);
        Assert.False(up.Swallow);
    }

    [Fact]
    public void ComboGesture_DoesNotBlockNextLonePress()
    {
        var tracker = new WinKeyGestureTracker();

        tracker.OnKeyDown(VkLeftWin);
        tracker.OnKeyDown(VkE);
        tracker.OnKeyUp(VkLeftWin);

        tracker.OnKeyDown(VkLeftWin);
        var up = tracker.OnKeyUp(VkLeftWin);

        Assert.True(up.Trigger);
    }

    [Fact]
    public void Reset_ClearsHeldState()
    {
        var tracker = new WinKeyGestureTracker();

        tracker.OnKeyDown(VkLeftWin);
        tracker.Reset();
        var up = tracker.OnKeyUp(VkLeftWin);

        Assert.False(tracker.IsWinHeld);
        Assert.False(up.Trigger);
    }
}
