using DevEnv.Core.Input;

namespace DevEnv.Tests;

public class HotkeyCaptureStateTests
{
    [Fact]
    public void Begin_And_Dispose_ToggleCaptureState()
    {
        Assert.False(HotkeyCaptureState.IsCapturing);

        using (HotkeyCaptureState.Begin())
        {
            Assert.True(HotkeyCaptureState.IsCapturing);
        }

        Assert.False(HotkeyCaptureState.IsCapturing);
    }

    [Fact]
    public void NestedScopes_StayCaptureUntilLastDispose()
    {
        IDisposable outer = HotkeyCaptureState.Begin();
        IDisposable inner = HotkeyCaptureState.Begin();

        inner.Dispose();
        Assert.True(HotkeyCaptureState.IsCapturing);

        outer.Dispose();
        Assert.False(HotkeyCaptureState.IsCapturing);
    }

    [Fact]
    public void Changed_FiresOnlyOnTransitions()
    {
        var events = new List<bool>();
        void Handler(bool value) => events.Add(value);

        HotkeyCaptureState.Changed += Handler;
        try
        {
            IDisposable outer = HotkeyCaptureState.Begin();
            IDisposable inner = HotkeyCaptureState.Begin();
            inner.Dispose();
            outer.Dispose();
        }
        finally
        {
            HotkeyCaptureState.Changed -= Handler;
        }

        Assert.Equal(new[] { true, false }, events);
    }

    [Fact]
    public void DoubleDispose_IsIgnored()
    {
        IDisposable scope = HotkeyCaptureState.Begin();
        scope.Dispose();
        scope.Dispose();

        Assert.False(HotkeyCaptureState.IsCapturing);
    }
}
