using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ProgramBox.Models;
using ProgramBox.Utils;

namespace ProgramBox.Views
{
    public partial class StartupManagerWindow : Window
    {
        private static StartupManagerWindow? _instance;

        private List<StartupEntry> _startupEntries = new();
        private List<StartupEntry> _serviceEntries = new();
        private List<StartupEntry> _taskEntries = new();
        private bool _isLoading;

        public StartupManagerWindow()
        {
            InitializeComponent();
            Opened += async (_, _) => await LoadAllEntriesAsync();
            Closed += (_, _) =>
            {
                if (ReferenceEquals(_instance, this))
                    _instance = null;
            };
        }

        /// <summary>
        /// 打开或激活已有的启动项管理窗口（非模态）
        /// </summary>
        public static void ShowOrActivate(Window? owner)
        {
            if (_instance != null)
            {
                if (!_instance.IsVisible)
                {
                    if (owner != null)
                        _instance.Show(owner);
                    else
                        _instance.Show();
                }
                _instance.WindowState = WindowState.Normal;
                _instance.Activate();
                return;
            }

            _instance = new StartupManagerWindow();
            if (owner != null)
                _instance.Show(owner);
            else
                _instance.Show();
        }

        private void SetStatus(string text)
        {
            if (StatusText != null)
                StatusText.Text = text;
        }

        private void SetLoading(bool loading)
        {
            _isLoading = loading;
            MainTabControl.IsEnabled = !loading;
            Cursor = loading
                ? new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Wait)
                : Avalonia.Input.Cursor.Default;
        }

        private async Task LoadAllEntriesAsync()
        {
            if (_isLoading)
                return;

            SetLoading(true);
            SetStatus("正在加载数据...");

            try
            {
                var all = await StartupManagerService.ListEntriesAsync();

                _startupEntries = all.Where(e =>
                    e.Location is StartupLocation.Registry
                        or StartupLocation.StartupFolder
                        or StartupLocation.DisabledRegistry
                        or StartupLocation.DisabledFolder).ToList();
                _serviceEntries = all.Where(e => e.Location == StartupLocation.Service).ToList();
                _taskEntries = all.Where(e => e.Location == StartupLocation.ScheduledTask).ToList();

                UpdateStartupGrid();
                UpdateServiceGrid();
                UpdateTaskGrid();

                SetStatus($"已加载 启动项:{_startupEntries.Count} 服务:{_serviceEntries.Count} 计划任务:{_taskEntries.Count}");
            }
            catch (Exception ex)
            {
                SetStatus("加载失败");
                await ShowMessageAsync("加载数据失败: " + ex.Message, "错误");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void UpdateStartupGrid()
        {
            var q = StartupSearchBox?.Text?.Trim();
            var filtered = _startupEntries.AsEnumerable();
            if (!string.IsNullOrEmpty(q))
            {
                filtered = filtered.Where(e =>
                    e.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    e.Command.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            var list = filtered.OrderBy(e => e.Name).ToList();
            StartupGrid.ItemsSource = list;
            if (!_isLoading)
                SetStatus($"启动项: 显示 {list.Count} / {_startupEntries.Count} 条");
        }

        private void UpdateServiceGrid()
        {
            var q = ServiceSearchBox?.Text?.Trim();
            var filtered = _serviceEntries.AsEnumerable();
            if (!string.IsNullOrEmpty(q))
            {
                filtered = filtered.Where(e =>
                    e.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    e.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    e.Command.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            var list = filtered.OrderBy(e => e.Name).ToList();
            ServiceGrid.ItemsSource = list;
            if (!_isLoading)
                SetStatus($"服务: 显示 {list.Count} / {_serviceEntries.Count} 条");
        }

        private void UpdateTaskGrid()
        {
            var q = TaskSearchBox?.Text?.Trim();
            var filtered = _taskEntries.AsEnumerable();
            if (!string.IsNullOrEmpty(q))
            {
                filtered = filtered.Where(e =>
                    e.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            var list = filtered.OrderBy(e => e.Name).ToList();
            TaskGrid.ItemsSource = list;
            if (!_isLoading)
                SetStatus($"计划任务: 显示 {list.Count} / {_taskEntries.Count} 条");
        }

        private void StartupSearchBox_TextChanged(object? sender, TextChangedEventArgs e) => UpdateStartupGrid();
        private void ServiceSearchBox_TextChanged(object? sender, TextChangedEventArgs e) => UpdateServiceGrid();
        private void TaskSearchBox_TextChanged(object? sender, TextChangedEventArgs e) => UpdateTaskGrid();

        private async void StartupRefresh_Click(object? sender, RoutedEventArgs e) => await LoadAllEntriesAsync();
        private async void ServiceRefresh_Click(object? sender, RoutedEventArgs e) => await LoadAllEntriesAsync();
        private async void TaskRefresh_Click(object? sender, RoutedEventArgs e) => await LoadAllEntriesAsync();

        private async void StartupAdd_Click(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择要添加的启动程序",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("可执行文件") { Patterns = new[] { "*.exe" } },
                    new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count == 0)
                return;

            var path = files[0].Path.LocalPath;
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var cmd = $"\"{path}\"";
            if (StartupManagerService.AddRegistryEntry(name, cmd))
            {
                await LoadAllEntriesAsync();
                SetStatus("已添加启动项: " + name);
            }
            else
            {
                await ShowMessageAsync("添加启动项失败", "错误");
            }
        }

        private async void StartupEnable_Click(object? sender, RoutedEventArgs e)
        {
            if (StartupGrid.SelectedItem is not StartupEntry sel)
            {
                await ShowMessageAsync("请先选择一个启动项", "提示");
                return;
            }

            if (StartupManagerService.EnableEntry(sel))
            {
                await LoadAllEntriesAsync();
                SetStatus("已启用: " + sel.Name);
            }
            else
            {
                await ShowMessageAsync("启用失败", "错误");
            }
        }

        private async void StartupDisable_Click(object? sender, RoutedEventArgs e)
        {
            if (StartupGrid.SelectedItem is not StartupEntry sel)
            {
                await ShowMessageAsync("请先选择一个启动项", "提示");
                return;
            }

            if (StartupManagerService.DisableEntry(sel))
            {
                await LoadAllEntriesAsync();
                SetStatus("已禁用: " + sel.Name);
            }
            else
            {
                await ShowMessageAsync("禁用失败", "错误");
            }
        }

        private async void StartupRemove_Click(object? sender, RoutedEventArgs e)
        {
            if (StartupGrid.SelectedItem is not StartupEntry sel)
            {
                await ShowMessageAsync("请先选择一个启动项", "提示");
                return;
            }

            if (!await ShowConfirmAsync($"确认删除启动项「{sel.Name}」？", "确认"))
                return;

            if (StartupManagerService.RemoveEntry(sel))
            {
                await LoadAllEntriesAsync();
                SetStatus("已删除: " + sel.Name);
            }
            else
            {
                await ShowMessageAsync("删除失败", "错误");
            }
        }

        private async void ServiceDisable_Click(object? sender, RoutedEventArgs e)
        {
            if (ServiceGrid.SelectedItem is not StartupEntry sel)
            {
                await ShowMessageAsync("请先选择一个服务", "提示");
                return;
            }

            if (!await ShowConfirmAsync(
                    $"确认将服务「{sel.Name}」的启动类型改为手动？\n\n服务名: {sel.Id}\n（可能弹出 UAC 提示）",
                    "确认"))
                return;

            if (StartupManagerService.DisableEntry(sel))
            {
                await LoadAllEntriesAsync();
                SetStatus("已禁用服务自启: " + sel.Name);
            }
            else
            {
                await ShowMessageAsync("禁用失败（可能已取消 UAC，或权限不足）", "错误");
            }
        }

        private async void ServiceDelete_Click(object? sender, RoutedEventArgs e)
        {
            if (ServiceGrid.SelectedItem is not StartupEntry sel)
            {
                await ShowMessageAsync("请先选择一个服务", "提示");
                return;
            }

            if (!await ShowConfirmAsync(
                    $"警告：确认删除服务「{sel.Name}」？\n\n服务名: {sel.Id}\n\n删除系统服务可能导致系统不稳定，请谨慎操作！",
                    "危险操作"))
                return;

            if (StartupManagerService.RemoveEntry(sel))
            {
                await LoadAllEntriesAsync();
                SetStatus("已删除服务: " + sel.Name);
            }
            else
            {
                await ShowMessageAsync("删除失败（可能已取消 UAC，或权限不足）", "错误");
            }
        }

        private async void TaskDisable_Click(object? sender, RoutedEventArgs e)
        {
            if (TaskGrid.SelectedItem is not StartupEntry sel)
            {
                await ShowMessageAsync("请先选择一个计划任务", "提示");
                return;
            }

            if (!await ShowConfirmAsync($"确认禁用计划任务「{sel.Name}」？\n（可能弹出 UAC 提示）", "确认"))
                return;

            if (StartupManagerService.DisableEntry(sel))
            {
                await LoadAllEntriesAsync();
                SetStatus("已禁用计划任务: " + sel.Name);
            }
            else
            {
                await ShowMessageAsync("禁用失败（可能已取消 UAC，或权限不足）", "错误");
            }
        }

        private async void TaskDelete_Click(object? sender, RoutedEventArgs e)
        {
            if (TaskGrid.SelectedItem is not StartupEntry sel)
            {
                await ShowMessageAsync("请先选择一个计划任务", "提示");
                return;
            }

            if (!await ShowConfirmAsync($"确认删除计划任务「{sel.Name}」？\n（可能弹出 UAC 提示）", "确认"))
                return;

            if (StartupManagerService.RemoveEntry(sel))
            {
                await LoadAllEntriesAsync();
                SetStatus("已删除计划任务: " + sel.Name);
            }
            else
            {
                await ShowMessageAsync("删除失败（可能已取消 UAC，或权限不足）", "错误");
            }
        }

        private async Task ShowMessageAsync(string message, string title)
        {
            var dialog = CreateSimpleDialog(title, message, showCancel: false);
            await dialog.ShowDialog(this);
        }

        private async Task<bool> ShowConfirmAsync(string message, string title)
        {
            var dialog = CreateSimpleDialog(title, message, showCancel: true);
            var result = await dialog.ShowDialog<bool?>(this);
            return result == true;
        }

        private Window CreateSimpleDialog(string title, string message, bool showCancel)
        {
            var okButton = new Button
            {
                Content = showCancel ? "确定" : "关闭",
                MinWidth = 80,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Button? cancelButton = null;

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Avalonia.Thickness(0, 16, 0, 0)
            };

            if (showCancel)
            {
                cancelButton = new Button { Content = "取消", MinWidth = 80 };
                buttons.Children.Add(cancelButton);
            }

            buttons.Children.Add(okButton);

            var root = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = this.FindResource("TextPrimaryBrush") as IBrush
                    },
                    buttons
                }
            };

            var dialog = new Window
            {
                Title = title,
                Width = 440,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Content = root,
                Background = this.FindResource("BackgroundBrush") as IBrush
            };

            okButton.Click += (_, _) => dialog.Close(true);
            if (cancelButton != null)
                cancelButton.Click += (_, _) => dialog.Close(false);

            return dialog;
        }
    }
}
