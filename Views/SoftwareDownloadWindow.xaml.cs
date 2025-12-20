using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DevEnv.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Button = System.Windows.Controls.Button;

namespace DevEnv.Views
{
    public partial class SoftwareDownloadWindow : Window
    {
        private ObservableCollection<SoftwareInfo> _allSoftware = new ObservableCollection<SoftwareInfo>();
        private ObservableCollection<SoftwareInfo> _filteredSoftware = new ObservableCollection<SoftwareInfo>();
        private List<string> _categories = new List<string>();

        public SoftwareDownloadWindow()
        {
            InitializeComponent();
            LoadSoftwareConfig();
        }

        private void LoadSoftwareConfig()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "software_config.yaml");
                if (!File.Exists(configPath))
                {
                    UpdateStatus("配置文件不存在", true);
                    return;
                }

                var yamlContent = File.ReadAllText(configPath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(NullNamingConvention.Instance)
                    .Build();

                var config = deserializer.Deserialize<Dictionary<string, List<Dictionary<string, object>>>>(yamlContent);

                _allSoftware.Clear();
                _categories.Clear();
                _categories.Add("全部");

                foreach (var category in config)
                {
                    _categories.Add(category.Key);

                    foreach (var software in category.Value)
                    {
                        var softwareInfo = new SoftwareInfo
                        {
                            Name = software.ContainsKey("name") ? software["name"]?.ToString() ?? "" : "",
                            Description = software.ContainsKey("desc") ? software["desc"]?.ToString() ?? "" : "",
                            Icon = software.ContainsKey("icon") ? software["icon"]?.ToString() ?? "" : "",
                            Category = category.Key,
                            Versions = new List<SoftwareVersion>()
                        };

                        if (software.ContainsKey("versions") && software["versions"] is List<object> versions)
                        {
                            foreach (var versionObj in versions)
                            {
                                if (versionObj is Dictionary<string, object> versionDict)
                                {
                                    softwareInfo.Versions.Add(new SoftwareVersion
                                    {
                                        Version = versionDict.ContainsKey("version") ? versionDict["version"]?.ToString() ?? "" : "",
                                        Url = versionDict.ContainsKey("url") ? versionDict["url"]?.ToString() ?? "" : ""
                                    });
                                }
                            }
                        }

                        _allSoftware.Add(softwareInfo);
                    }
                }

                CmbCategory.ItemsSource = _categories;
                CmbCategory.SelectedIndex = 0;

                _filteredSoftware = new ObservableCollection<SoftwareInfo>(_allSoftware);
                SoftwareDataGrid.ItemsSource = _filteredSoftware;

                UpdateStatus($"已加载 {_allSoftware.Count} 个软件", false);
                UpdateCount();
            }
            catch (Exception ex)
            {
                UpdateStatus($"加载配置失败: {ex.Message}", true);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterSoftware();
        }

        private void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterSoftware();
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            FilterSoftware();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            CmbCategory.SelectedIndex = 0;
            CmbVersion.SelectedIndex = -1;
            FilterSoftware();
        }

        private void FilterSoftware()
        {
            var searchText = TxtSearch.Text?.ToLower() ?? "";
            var selectedCategory = CmbCategory.SelectedItem?.ToString() ?? "全部";

            var filtered = _allSoftware.Where(s =>
                (selectedCategory == "全部" || s.Category == selectedCategory) &&
                (string.IsNullOrWhiteSpace(searchText) ||
                 s.Name.ToLower().Contains(searchText) ||
                 s.Description.ToLower().Contains(searchText))
            ).ToList();

            _filteredSoftware.Clear();
            foreach (var software in filtered)
            {
                _filteredSoftware.Add(software);
            }

            UpdateStatus($"找到 {filtered.Count} 个软件", false);
            UpdateCount();
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SoftwareInfo software)
            {
                // 获取选中的版本
                var dataGridRow = FindVisualParent<DataGridRow>(button);
                if (dataGridRow != null)
                {
                    var dataGridCell = FindVisualParent<DataGridCell>(button);
                    if (dataGridCell != null)
                    {
                        var comboBox = FindVisualChild<ComboBox>(dataGridCell);
                        if (comboBox != null && comboBox.SelectedItem is SoftwareVersion version)
                        {
                            await DownloadSoftware(software, version, button);
                        }
                    }
                }
            }
        }

        private async Task DownloadSoftware(SoftwareInfo software, SoftwareVersion version, Button button)
        {
            try
            {
                button.IsEnabled = false;
                button.Content = "下载中...";

                // 创建下载目录
                var downloadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
                if (!Directory.Exists(downloadDir))
                {
                    Directory.CreateDirectory(downloadDir);
                }

                var fileName = $"{software.Name}_{version.Version}{Path.GetExtension(version.Url) ?? ".exe"}";
                var filePath = Path.Combine(downloadDir, fileName);

                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(version.Url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var receivedBytes = 0L;

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    receivedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = (int)(receivedBytes * 100 / totalBytes);
                        button.Content = $"下载中... {progress}%";
                    }
                    else
                    {
                        button.Content = $"下载中... {receivedBytes / 1024} KB";
                    }
                }

                button.Content = "完成";
                UpdateStatus($"下载完成: {filePath}", false);

                // 询问是否打开文件
                var result = MessageBox.Show($"软件已下载到:\n{filePath}\n\n是否打开文件夹？", "下载完成",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Process.Start("explorer.exe", downloadDir);
                }
            }
            catch (Exception ex)
            {
                button.Content = "下载";
                UpdateStatus($"下载失败: {ex.Message}", true);
                MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void UpdateStatus(string message, bool isError)
        {
            TxtStatus.Text = message;
            TxtStatus.Foreground = isError ?
                System.Windows.Media.Brushes.Red :
                System.Windows.Media.Brushes.Green;
        }

        private void UpdateCount()
        {
            TxtCount.Text = $"共 {_filteredSoftware.Count} 项";
        }

        // 辅助方法：查找父元素
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }

        // 辅助方法：查找子元素
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                    return found;
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }
    }
}
