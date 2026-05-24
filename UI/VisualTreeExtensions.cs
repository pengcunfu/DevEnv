using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace DevEnv.UI;

public static class VisualTreeExtensions
{
    public static T? FindVisualParent<T>(this Visual? child) where T : Visual
        => child?.GetVisualAncestors().OfType<T>().FirstOrDefault();

    public static T? FindVisualChild<T>(this Visual? parent) where T : Visual
        => parent?.GetVisualDescendants().OfType<T>().FirstOrDefault();
}
