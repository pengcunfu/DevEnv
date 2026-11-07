using DevEnv.Core.Input;

namespace DevEnv.Tests;

public sealed class AltKeyGestureTrackerTests
{
    private const int VkE = 0x45;
    private const int VkLeftAlt = AltKeyGestureTracker.VkLeftAlt;
    private const int VkRightAlt = AltKeyGestureTracker.VkRightAlt;

    [Fact]
    public void LoneLeftAltPress_TriggersOnKeyUp()
    {
        var tracker = new AltKeyGestureTracker();

        var down = tracker.OnKeyDown(VkLeftAlt);
        var up = tracker.OnKeyUp(VkLeftAlt);

        Assert.False(down.Trigger);
        Assert.True(up.Trigger);
        Assert.False(tracker.IsAltHeld);
    }

    [Fact]
    public void RightAlt_NeverTriggers()
    {
        var tracker = new AltKeyGestureTracker();

        tracker.OnKeyDown(VkRightAlt);
        var up = tracker.OnKeyUp(VkRightAlt);

        Assert.False(up.Trigger);
    }

    [Fact]
    public void LeftAltPlusOtherKey_DoesNotTrigger()
    {
        var tracker = new AltKeyGestureTracker();

        tracker.OnKeyDown(VkLeftAlt);
        tracker.OnKeyDown(VkE);
        var up = tracker.OnKeyUp(VkLeftAlt);

        // Alt+其它键：抬起必须放行，否则前台程序会认为 Alt 一直按着
        Assert.False(up.Trigger);
    }

    [Fact]
    public void OtherKeyReleasedBeforeAlt_StillCountsAsCombo()
    {
        var tracker = new AltKeyGestureTracker();

        tracker.OnKeyDown(VkLeftAlt);
        tracker.OnKeyDown(VkE);
        tracker.OnKeyUp(VkE);
        var up = tracker.OnKeyUp(VkLeftAlt);

        Assert.False(up.Trigger);
    }

    [Fact]
    public void LeftAltAutoRepeat_StillCountsAsLonePress()
    {
        var tracker = new AltKeyGestureTracker();

        tracker.OnKeyDown(VkLeftAlt);
        // 按住不放时系统会持续送重复的按下事件
        tracker.OnKeyDown(VkLeftAlt);
        tracker.OnKeyDown(VkLeftAlt);
        var up = tracker.OnKeyUp(VkLeftAlt);

        Assert.True(up.Trigger);
    }

    [Fact]
    public void BothAltKeys_CountAsCombo()
    {
        var tracker = new AltKeyGestureTracker();

        tracker.OnKeyDown(VkLeftAlt);
        tracker.OnKeyDown(VkRightAlt);
        var up = tracker.OnKeyUp(VkLeftAlt);

        Assert.False(up.Trigger);
    }

    [Fact]
    public void KeyUpWithoutAltDown_IsIgnored()
    {
        var tracker = new AltKeyGestureTracker();

        var result = tracker.OnKeyUp(VkE);

        Assert.False(result.Trigger);
    }

    [Fact]
    public void PlainKey_DoesNotEnterAltGesture()
    {
        var tracker = new AltKeyGestureTracker();

        tracker.OnKeyDown(VkE);
        var up = tracker.OnKeyUp(VkE);

        Assert.False(tracker.IsAltHeld);
        Assert.False(up.Trigger);
    }

    [Fact]
    public void ComboGesture_DoesNotBlockNextLonePress()
    {
        var tracker = new AltKeyGestureTracker();

        tracker.OnKeyDown(VkLeftAlt);
        tracker.OnKeyDown(VkE);
        tracker.OnKeyUp(VkLeftAlt);

        tracker.OnKeyDown(VkLeftAlt);
        var up = tracker.OnKeyUp(VkLeftAlt);

        Assert.True(up.Trigger);
    }

    [Fact]
    public void GenericAltKeyUp_EndsGesture()
    {
        var tracker = new AltKeyGestureTracker();

        tracker.OnKeyDown(VkLeftAlt);
        // 部分场景（注入按键、部分布局）抬起会报通用 Alt 码，必须照样结束手势
        var up = tracker.OnKeyUp(AltKeyGestureTracker.VkAlt);

        Assert.True(up.Trigger);
        Assert.False(tracker.IsAltHeld);
    }

    [Fact]
    public void RightAltKeyUp_EndsGestureWithoutTrigger()
    {
        var tracker = new AltKeyGestureTracker();

        tracker.OnKeyDown(VkLeftAlt);
        tracker.OnKeyDown(VkRightAlt);
        var up = tracker.OnKeyUp(VkRightAlt);

        // 左右 Alt 同时按属于组合键，不触发，但手势要收尾
        Assert.False(up.Trigger);
        Assert.False(tracker.IsAltHeld);
    }

    [Fact]
    public void Reset_ClearsHeldState()
    {
        var tracker = new AltKeyGestureTracker();

        tracker.OnKeyDown(VkLeftAlt);
        tracker.Reset();
        var up = tracker.OnKeyUp(VkLeftAlt);

        Assert.False(tracker.IsAltHeld);
        Assert.False(up.Trigger);
    }
}
