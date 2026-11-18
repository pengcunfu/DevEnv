using System.Windows;
using System.Windows.Media;

namespace DevEnv.Host.Infrastructure;

/// <summary>可视树查找辅助：事件处理里从 OriginalSource 找所在行等。</summary>
internal static class VisualTreeSearch
{
    public static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
                return match;
            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    /// <summary>在可视树里向下找第一个指定类型的元素（含根元素本身）。</summary>
    public static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is null)
            return null;
        if (root is T match)
            return match;

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            if (FindDescendant<T>(VisualTreeHelper.GetChild(root, i)) is { } found)
                return found;
        }

        return null;
    }
}
