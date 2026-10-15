using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DevEnv.ViewModels;

namespace DevEnv.Views;

public partial class HashCalculatorWindow : Window
{
    public HashCalculatorWindow()
    {
        InitializeComponent();
        DataContext = new HashCalculatorViewModel();
        HashTypeComboBox.SelectedIndex = 2;
    }

    private void Border_DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
            if (sender is Border border)
            {
                border.Background = Brushes.LightBlue;
                border.Opacity = 0.8;
            }
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void Border_DragLeave(object? sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = Brushes.Transparent;
            border.Opacity = 1.0;
        }
    }

    private void Border_Drop(object? sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = Brushes.Transparent;
            border.Opacity = 1.0;
        }

        if (!e.Data.Contains(DataFormats.Files))
            return;

        var path = e.Data.GetFiles()?.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrEmpty(path))
            return;

        if (DataContext is not HashCalculatorViewModel viewModel)
            return;

        MainTabControl.SelectedItem = FileHashTab;
        viewModel.FilePath = path;
        viewModel.CalculateHashCommand.Execute(null);
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not HashCalculatorViewModel viewModel)
            return;

        if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (MainTabControl.SelectedItem == FileHashTab)
                viewModel.PastePathCommand.Execute(null);
        }
        else if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control)
                 && viewModel.HashResults.Count > 0)
        {
            viewModel.CopyHashCommand.Execute(viewModel.HashResults[0].Hash);
        }
    }
}
