namespace DevEnv.Core.SigXor.Toolkit;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static PixelRect FromLtrb(int left, int top, int right, int bottom) =>
        new(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));

    public bool Contains(int x, int y) =>
        x >= X && x < Right && y >= Y && y < Bottom;

    public bool Intersects(PixelRect other) =>
        other.X < Right && X < other.Right && other.Y < Bottom && Y < other.Bottom;

    public PixelRect Intersect(PixelRect other)
    {
        int left = Math.Max(X, other.X);
        int top = Math.Max(Y, other.Y);
        int right = Math.Min(Right, other.Right);
        int bottom = Math.Min(Bottom, other.Bottom);
        return right <= left || bottom <= top ? default : FromLtrb(left, top, right, bottom);
    }

    public PixelRect Union(PixelRect other)
    {
        int left = Math.Min(X, other.X);
        int top = Math.Min(Y, other.Y);
        int right = Math.Max(Right, other.Right);
        int bottom = Math.Max(Bottom, other.Bottom);
        return FromLtrb(left, top, right, bottom);
    }

    public static PixelRect Union(IEnumerable<PixelRect> rects)
    {
        PixelRect? result = null;
        foreach (PixelRect rect in rects)
        {
            if (rect.IsEmpty) continue;
            result = result is null ? rect : result.Value.Union(rect);
        }
        return result ?? default;
    }

    public static PixelRect Normalize(int x0, int y0, int x1, int y1) =>
        FromLtrb(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));

    public static int PhysicalToDip(int value, double scale) =>
        (int)Math.Round(value / (scale <= 0 ? 1.0 : scale));

    public static int DipToPhysical(int value, double scale) =>
        (int)Math.Round(value * (scale <= 0 ? 1.0 : scale));

    public static PixelRect PhysicalToDip(PixelRect rect, double scale)
    {
        double safe = scale <= 0 ? 1.0 : scale;
        int x = (int)Math.Round(rect.X / safe);
        int y = (int)Math.Round(rect.Y / safe);
        int w = Math.Max(1, (int)Math.Round(rect.Width / safe));
        int h = Math.Max(1, (int)Math.Round(rect.Height / safe));
        return new PixelRect(x, y, w, h);
    }

    public static PixelRect DipToPhysical(PixelRect rect, double scale)
    {
        double safe = scale <= 0 ? 1.0 : scale;
        int x = (int)Math.Round(rect.X * safe);
        int y = (int)Math.Round(rect.Y * safe);
        int w = Math.Max(1, (int)Math.Round(rect.Width * safe));
        int h = Math.Max(1, (int)Math.Round(rect.Height * safe));
        return new PixelRect(x, y, w, h);
    }

    public static PixelRect PlaceNear(
        PixelRect anchor,
        PixelRect screenArea,
        int width,
        int height,
        int gap = 8,
        int margin = 4)
    {
        int x = anchor.X + (anchor.Width - width) / 2;
        int y = anchor.Bottom + gap;

        if (y + height > screenArea.Bottom - margin)
            y = anchor.Y - height - gap;

        x = Math.Clamp(x, screenArea.X + margin, Math.Max(screenArea.X + margin, screenArea.Right - width - margin));
        y = Math.Clamp(y, screenArea.Y + margin, Math.Max(screenArea.Y + margin, screenArea.Bottom - height - margin));
        return new PixelRect(x, y, width, height);
    }
}
