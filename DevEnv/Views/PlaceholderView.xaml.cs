using System.Windows.Controls;

namespace DevEnv.Host.Views;

/// <summary>
/// 尚未接入的页面占位（批次 4~7 会逐个替换为真实视图）。
/// </summary>
public partial class PlaceholderView : UserControl
{
    public PlaceholderView(string title, string hint = "该页面将在后续批次接入")
    {
        InitializeComponent();
        TitleText.Text = title;
        HintText.Text = hint;
    }
}