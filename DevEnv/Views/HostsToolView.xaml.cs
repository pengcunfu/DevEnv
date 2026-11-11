using System.Windows;
using System.Windows.Controls;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

public partial class HostsToolView : UserControl
{
    public HostsToolView()
    {
        InitializeComponent();
    }

    private HostsToolViewModel ViewModel => (HostsToolViewModel)DataContext;

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        Editor.CommitPendingEdits();
        if (ViewModel.SaveCommand.CanExecute(null))
            ViewModel.SaveCommand.Execute(null);
    }

    private void OnReloadClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.LoadCommand.CanExecute(null))
            ViewModel.LoadCommand.Execute(null);
    }
}
