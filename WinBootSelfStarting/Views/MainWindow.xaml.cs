using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using WinBootSelfStarting.Models;
using WinBootSelfStarting.Services;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows.Media;

namespace WinBootSelfStarting
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<StartupEntry> _startupEntries = new();
        private List<StartupEntry> _serviceEntries = new();
        private List<StartupEntry> _taskEntries = new();
        private bool _isLoading = false;

        public MainWindow()
        {
            InitializeComponent();

            // Add search text changed handlers
            StartupSearchBox.TextChanged += (s, e) => UpdateStartupGrid();
            ServiceSearchBox.TextChanged += (s, e) => UpdateServiceGrid();
            TaskSearchBox.TextChanged += (s, e) => UpdateTaskGrid();

            // Load data asynchronously
            Loaded += async (s, e) => await LoadAllEntriesAsync();
        }

        private void SetStatus(string text)
        {
            if (StatusText != null)
                StatusText.Text = text;
        }

        private async Task LoadAllEntriesAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                // Show loading status
                SetStatus("正在加载数据...");
                SetLoadingState(true);

                // Load data asynchronously
                var allEntries = await StartupManager.ListEntriesAsync();

                // Split entries by type
                _startupEntries = allEntries.Where(e =>
                    e.Location == StartupLocation.Registry ||
                    e.Location == StartupLocation.StartupFolder ||
                    e.Location == StartupLocation.DisabledRegistry ||
                    e.Location == StartupLocation.DisabledFolder).ToList();

                _serviceEntries = allEntries.Where(e => e.Location == StartupLocation.Service).ToList();
                _taskEntries = allEntries.Where(e => e.Location == StartupLocation.ScheduledTask).ToList();

                // Update grids
                UpdateStartupGrid();
                UpdateServiceGrid();
                UpdateTaskGrid();

                SetStatus($"已加载 启动项:{_startupEntries.Count} 服务:{_serviceEntries.Count} 计划任务:{_taskEntries.Count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载数据失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("加载失败");
            }
            finally
            {
                _isLoading = false;
                SetLoadingState(false);
            }
        }

        private void LoadAllEntries()
        {
            // For backwards compatibility
            Task.Run(async () => await LoadAllEntriesAsync());
        }

        private void SetLoadingState(bool isLoading)
        {
            if (isLoading)
            {
                // Disable controls during loading
                StartupRefreshButton.IsEnabled = false;
                ServiceRefreshButton.IsEnabled = false;
                TaskRefreshButton.IsEnabled = false;
                MainTabControl.IsEnabled = false;

                // Change cursor to wait
                Cursor = System.Windows.Input.Cursors.Wait;
            }
            else
            {
                // Enable controls after loading
                StartupRefreshButton.IsEnabled = true;
                ServiceRefreshButton.IsEnabled = true;
                TaskRefreshButton.IsEnabled = true;
                MainTabControl.IsEnabled = true;

                // Restore cursor
                Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private void UpdateStartupGrid()
        {
            if (StartupGrid == null) return;

            var q = StartupSearchBox?.Text?.Trim();
            var filtered = _startupEntries.AsEnumerable();

            if (!string.IsNullOrEmpty(q))
            {
                filtered = filtered.Where(e =>
                    (e.Name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (e.Command ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var list = filtered.OrderBy(e => e.Name).ToList();
            StartupGrid.ItemsSource = list;
            SetStatus($"启动项: 显示 {list.Count} / {_startupEntries.Count} 条");
        }

        private void UpdateServiceGrid()
        {
            if (ServiceGrid == null) return;

            var q = ServiceSearchBox?.Text?.Trim();
            var filtered = _serviceEntries.AsEnumerable();

            if (!string.IsNullOrEmpty(q))
            {
                filtered = filtered.Where(e =>
                    (e.Name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (e.Id ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (e.Command ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var list = filtered.OrderBy(e => e.Name).ToList();
            ServiceGrid.ItemsSource = list;
            SetStatus($"服务: 显示 {list.Count} / {_serviceEntries.Count} 条");
        }

        private void UpdateTaskGrid()
        {
            if (TaskGrid == null) return;

            var q = TaskSearchBox?.Text?.Trim();
            var filtered = _taskEntries.AsEnumerable();

            if (!string.IsNullOrEmpty(q))
            {
                filtered = filtered.Where(e =>
                    (e.Name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var list = filtered.OrderBy(e => e.Name).ToList();
            TaskGrid.ItemsSource = list;
            SetStatus($"计划任务: 显示 {list.Count} / {_taskEntries.Count} 条");
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabControl.SelectedItem == StartupTab)
                UpdateStartupGrid();
            else if (MainTabControl.SelectedItem == ServiceTab)
                UpdateServiceGrid();
            else if (MainTabControl.SelectedItem == TaskTab)
                UpdateTaskGrid();
        }

        // Startup Tab handlers
        private async void StartupRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllEntriesAsync();
        }

        private async void StartupAddButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog(this) == true)
            {
                var path = dlg.FileName;
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                var cmd = '"' + path + '"';
                var ok = StartupManager.AddRegistryEntry(name, cmd);

                if (ok)
                {
                    await LoadAllEntriesAsync();
                    SetStatus("已添加启动项: " + name);
                }
                else
                {
                    MessageBox.Show("添加启动项失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void StartupEnableButton_Click(object sender, RoutedEventArgs e)
        {
            var sel = StartupGrid.SelectedItem as StartupEntry;
            if (sel == null)
            {
                MessageBox.Show("请先选择一个启动项", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ok = StartupManager.EnableEntry(sel);
            if (ok)
            {
                await LoadAllEntriesAsync();
                SetStatus("已启用: " + sel.Name);
            }
            else
            {
                MessageBox.Show("启用失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StartupDisableButton_Click(object sender, RoutedEventArgs e)
        {
            var sel = StartupGrid.SelectedItem as StartupEntry;
            if (sel == null)
            {
                MessageBox.Show("请先选择一个启动项", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ok = StartupManager.DisableEntry(sel);
            if (ok)
            {
                await LoadAllEntriesAsync();
                SetStatus("已禁用: " + sel.Name);
            }
            else
            {
                MessageBox.Show("禁用失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StartupRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var sel = StartupGrid.SelectedItem as StartupEntry;
            if (sel == null)
            {
                MessageBox.Show("请先选择一个启动项", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var res = MessageBox.Show($"确认删除启动项 '{sel.Name}' ?", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            var ok = StartupManager.RemoveEntry(sel);
            if (ok)
            {
                await LoadAllEntriesAsync();
                SetStatus("已删除: " + sel.Name);
            }
            else
            {
                MessageBox.Show("删除失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Service Tab handlers
        private async void ServiceRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllEntriesAsync();
        }

        private async void ServiceDisableButton_Click(object sender, RoutedEventArgs e)
        {
            var sel = ServiceGrid.SelectedItem as StartupEntry;
            if (sel == null)
            {
                MessageBox.Show("请先选择一个服务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var res = MessageBox.Show($"确认将服务 '{sel.Name}' 的启动类型改为手动?\n\n服务名: {sel.Id}",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            var ok = StartupManager.DisableEntry(sel);
            if (ok)
            {
                await LoadAllEntriesAsync();
                SetStatus("已禁用服务自启: " + sel.Name);
            }
            else
            {
                MessageBox.Show("禁用失败，请确保程序以管理员身份运行", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ServiceDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var sel = ServiceGrid.SelectedItem as StartupEntry;
            if (sel == null)
            {
                MessageBox.Show("请先选择一个服务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var res = MessageBox.Show($"警告：确认要删除服务 '{sel.Name}' ?\n\n服务名: {sel.Id}\n\n删除系统服务可能导致系统不稳定，请谨慎操作！",
                "危险操作", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;

            var ok = StartupManager.RemoveEntry(sel);
            if (ok)
            {
                await LoadAllEntriesAsync();
                SetStatus("已删除服务: " + sel.Name);
            }
            else
            {
                MessageBox.Show("删除失败，请确保程序以管理员身份运行", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Task Tab handlers
        private async void TaskRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllEntriesAsync();
        }

        private async void TaskDisableButton_Click(object sender, RoutedEventArgs e)
        {
            var sel = TaskGrid.SelectedItem as StartupEntry;
            if (sel == null)
            {
                MessageBox.Show("请先选择一个计划任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var res = MessageBox.Show($"确认禁用计划任务 '{sel.Name}' ?", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            var ok = StartupManager.DisableEntry(sel);
            if (ok)
            {
                await LoadAllEntriesAsync();
                SetStatus("已禁用计划任务: " + sel.Name);
            }
            else
            {
                MessageBox.Show("禁用失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TaskDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var sel = TaskGrid.SelectedItem as StartupEntry;
            if (sel == null)
            {
                MessageBox.Show("请先选择一个计划任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var res = MessageBox.Show($"确认删除计划任务 '{sel.Name}' ?", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            var ok = StartupManager.RemoveEntry(sel);
            if (ok)
            {
                await LoadAllEntriesAsync();
                SetStatus("已删除计划任务: " + sel.Name);
            }
            else
            {
                MessageBox.Show("删除失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
