using System.Globalization;

namespace DevEnv.Core.SigXor;

/// <summary>用于更新清单的语义版本比较，忽略 prerelease/build 元数据。</summary>
public readonly record struct VersionText(int Major, int Minor, int Patch) : IComparable<VersionText>
{
    public static bool TryParse(string? text, out VersionText version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string core = text.Trim();
        int suffix = core.IndexOfAny(['-', '+']);
        if (suffix >= 0) core = core[..suffix];

        string[] parts = core.Split('.');
        if (parts.Length is < 1 or > 3) return false;

        int[] values = new int[3];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                || value < 0)
                return false;
            values[i] = value;
        }

        version = new VersionText(values[0], values[1], values[2]);
        return true;
    }

    public int CompareTo(VersionText other)
    {
        int major = Major.CompareTo(other.Major);
        if (major != 0) return major;
        int minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public static bool operator <(VersionText left, VersionText right) => left.CompareTo(right) < 0;
    public static bool operator >(VersionText left, VersionText right) => left.CompareTo(right) > 0;
    public static bool operator <=(VersionText left, VersionText right) => left.CompareTo(right) <= 0;
    public static bool operator >=(VersionText left, VersionText right) => left.CompareTo(right) >= 0;
}
