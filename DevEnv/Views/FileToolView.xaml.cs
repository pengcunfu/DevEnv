using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>文件解锁与粉碎。支持把文件 / 文件夹直接拖进粉碎列表。</summary>
public partial class FileToolView : UserControl
{
    private readonly DragEventHandler _onDragOver;
    private readonly DragEventHandler _onDrop;

    public FileToolView()
    {
        InitializeComponent();

        _onDragOver = OnDragOver;
        _onDrop = OnDrop;
        // 子控件（DataGrid / TextBox）也会处理拖放，这里连已处理的事件一起接住。
        AddHandler(DragDrop.DragOverEvent, _onDragOver, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent, _onDrop, handledEventsToo: true);

        Unloaded += (_, _) =>
        {
            (DataContext as FileToolViewModel)?.Dispose();
            RemoveHandler(DragDrop.DragOverEvent, _onDragOver);
            RemoveHandler(DragDrop.DropEvent, _onDrop);
        };
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (DataContext is not FileToolViewModel || !e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is FileToolViewModel viewModel
            && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            viewModel.AddShredPaths(paths);
            Tabs.SelectedIndex = 1;
            e.Handled = true;
        }
    }

    private void OnLockPathKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not FileToolViewModel viewModel)
            return;

        if (viewModel.ScanLockCommand.CanExecute(null))
            viewModel.ScanLockCommand.Execute(null);
        e.Handled = true;
    }

    private void OnShredInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not FileToolViewModel viewModel)
            return;

        if (viewModel.AddShredInputCommand.CanExecute(null))
            viewModel.AddShredInputCommand.Execute(null);
        e.Handled = true;
    }
}
