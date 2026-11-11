using System.Windows;
using System.Windows.Controls;

namespace DevEnv.Host.Controls;

/// <summary>
/// 固定尺寸子项的换行面板。启动器图标网格用它排版：按可用宽度算列数，
/// 行内左对齐（贴近 Windows 11 开始菜单「已固定」），避免居中后左右空出大块边距。
/// </summary>
public sealed class CenteredWrapPanel : Panel
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(CenteredWrapPanel),
        new FrameworkPropertyMetadata(90d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight), typeof(double), typeof(CenteredWrapPanel),
        new FrameworkPropertyMetadata(112d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var itemSize = new Size(ItemWidth, ItemHeight);
        foreach (UIElement child in InternalChildren)
            child.Measure(itemSize);

        var count = InternalChildren.Count;
        if (count == 0)
            return new Size(0, 0);

        // 按内容收窄：文件夹浮层只有两三项时不要被父级 MaxWidth 撑满整行
        var constraint = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width;
        var maxColumns = double.IsInfinity(constraint)
            ? count
            : Math.Max(1, (int)(constraint / ItemWidth));
        var columns = Math.Max(1, Math.Min(maxColumns, count));
        var rows = (count + columns - 1) / columns;
        var width = columns * ItemWidth;
        return new Size(width, rows * ItemHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnsFor(finalSize.Width);

        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            var rect = new Rect(
                column * ItemWidth,
                row * ItemHeight,
                ItemWidth,
                ItemHeight);
            InternalChildren[i].Arrange(rect);
        }

        return finalSize;
    }

    private int ColumnsFor(double width)
    {
        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
            return 1;
        return Math.Max(1, (int)(width / ItemWidth));
    }
}
