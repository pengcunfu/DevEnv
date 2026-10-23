namespace DevEnv.Core.ProcessWatch.Models;

public static class ByteFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
            bytes = 0;

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} {Units[unit]}"
            : $"{value:0.##} {Units[unit]}";
    }
}
