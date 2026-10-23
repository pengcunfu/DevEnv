using System.Windows;
using System.Windows.Controls;

namespace DevBox.Host.Views;

/// <summary>主壳里的熔岩翻译入口页。翻译站本体在 TranslatorWindow（WebView2）。</summary>
public partial class TranslatorEntryView : UserControl
{
    public TranslatorEntryView()
    {
        InitializeComponent();
        DataContext = new ViewModels.TranslatorEntryViewModel();
    }

    private void OnSiteChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string id } && DataContext is ViewModels.TranslatorEntryViewModel vm)
        {
            vm.CurrentSiteId = id;
            vm.SetDefaultSite();
        }
    }
}