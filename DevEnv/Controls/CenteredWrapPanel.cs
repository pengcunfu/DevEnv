using System.Windows;
using System.Windows.Controls;

namespace DevEnv.Host.Controls;

/// <summary>
/// 固定尺寸子项、逐行居中的换行面板。启动器的图标网格用它排版：
/// WPF 自带 WrapPanel 会把每一行左对齐，项目少时整块偏左；这里按可用宽度
/// 算出列数，再把每一行在水平方向居中，视觉上更接近 Launchpad。
/// </summary>
public sealed class CenteredWrapPanel : Panel
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(CenteredWrapPanel),
        new FrameworkPropertyMetadata(132d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight), typeof(double), typeof(CenteredWrapPanel),
        new FrameworkPropertyMetadata(150d, FrameworkPropertyMetadataOptions.AffectsMeasure));

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

        var width = double.IsInfinity(availableSize.Width) ? ItemWidth : availableSize.Width;
        var columns = ColumnsFor(width);
        var rows = (InternalChildren.Count + columns - 1) / columns;
        return new Size(width, rows * ItemHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnsFor(finalSize.Width);
        var rowWidth = columns * ItemWidth;
        var leftOffset = Math.Max(0, (finalSize.Width - rowWidth) / 2);

        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            var rect = new Rect(
                leftOffset + column * ItemWidth,
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
