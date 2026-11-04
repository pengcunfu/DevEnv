using DevEnv.Core.SigXor.Toolkit;

namespace DevEnv.Tests.SigXor;

public class ScreenGeometryTests
{
    [Fact]
    public void Union_HandlesNegativeCoordinates()
    {
        PixelRect left = new(-1920, 0, 1920, 1080);
        PixelRect right = new(0, 0, 2560, 1440);

        PixelRect union = PixelRect.Union([left, right]);

        Assert.Equal(-1920, union.X);
        Assert.Equal(0, union.Y);
        Assert.Equal(4480, union.Width);
        Assert.Equal(1440, union.Height);
    }

    [Fact]
    public void Intersect_ReturnsEmptyWhenDisjoint()
    {
        PixelRect a = new(0, 0, 100, 100);
        PixelRect b = new(200, 200, 100, 100);

        Assert.True(a.Intersect(b).IsEmpty);
    }

    [Fact]
    public void Normalize_OrdersPoints()
    {
        PixelRect rect = PixelRect.Normalize(120, 90, 20, 10);

        Assert.Equal(new PixelRect(20, 10, 100, 80), rect);
    }

    [Fact]
    public void PhysicalDip_RoundTrips()
    {
        PixelRect physical = new(100, 200, 1920, 1080);

        PixelRect dip = PixelRect.PhysicalToDip(physical, 1.5);
        PixelRect roundTrip = PixelRect.DipToPhysical(dip, 1.5);

        Assert.Equal(67, dip.X);
        Assert.Equal(1280, dip.Width);
        Assert.Equal(1920, roundTrip.Width);
    }

    [Fact]
    public void PlaceNear_PrefersBelowAndClamps()
    {
        PixelRect area = new(0, 0, 1000, 800);
        PixelRect anchor = new(900, 700, 80, 80);

        PixelRect placed = PixelRect.PlaceNear(anchor, area, 300, 60);

        Assert.Equal(696, placed.X);
        Assert.Equal(632, placed.Y);
    }

    [Fact]
    public void PlaceNear_FlipsAboveWhenNoRoomBelow()
    {
        PixelRect area = new(0, 0, 1000, 800);
        PixelRect anchor = new(400, 700, 200, 80);

        PixelRect placed = PixelRect.PlaceNear(anchor, area, 200, 40);

        Assert.Equal(652, placed.Y);
    }
}
