using System.Globalization;

namespace DevEnv.Tests;

/// <summary>
/// 临时切到不变区域性：格式化断言里的小数点与千分位不能受运行机器的语言影响。
/// </summary>
internal readonly struct InvariantCultureScope : IDisposable
{
    private readonly CultureInfo _previous;

    public InvariantCultureScope()
    {
        _previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}