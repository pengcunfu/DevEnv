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
}
