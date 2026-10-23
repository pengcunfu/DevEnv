using DevEnv.Core.Clipboard.Services;

namespace DevEnv.Tests;

public sealed class HistoryTimestampTests
{
    private const string Format = "yyyy-MM-dd HH:mm:ss";

    [Fact]
    public void TodayStartTimestamp_IsMidnightWithExpectedFormat()
    {
        var value = HistoryService.TodayStartTimestamp();

        Assert.True(DateTime.TryParseExact(value, Format, null, System.Globalization.DateTimeStyles.None, out var parsed));
        Assert.Equal(DateTime.Today, parsed);
    }

    [Fact]
    public void DaysAgoTimestamp_IsBeforeTodayStart()
    {
        var week = HistoryService.DaysAgoTimestamp(7);
        var month = HistoryService.DaysAgoTimestamp(30);
        var today = HistoryService.TodayStartTimestamp();

        Assert.True(string.CompareOrdinal(week, today) < 0, "7 天前应当早于今天零点");
        Assert.True(string.CompareOrdinal(month, week) < 0, "30 天前应当早于 7 天前");
    }

    [Fact]
    public void Timestamps_AreLexicographicallyComparable()
    {
        var today = HistoryService.TodayStartTimestamp();
        var later = DateTime.Now.AddMinutes(1).ToString(Format);

        Assert.True(string.CompareOrdinal(today, later) < 0);
    }
}