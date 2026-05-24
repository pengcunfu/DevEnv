using DevEnv.UI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using DevEnv.Models;
using DevEnv.Services;

namespace DevEnv.Views
{
    public partial class EnvironmentScannerWindow : Window
    {
        private readonly ObservableCollection<InstalledEnvironment> _environments = new();

        public EnvironmentScannerWindow()
        {
            InitializeComponent();
            EnvGrid.ItemsSource = _environments;
        }

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            BtnScan.IsEnabled = false;
            TxtStatus.Text = "正在扫描系统环境...";
            _environments.Clear();

            try
            {
                var results = await AppServices.EnvironmentScanner.ScanAllAsync();
                foreach (var env in results)
                    _environments.Add(env);

                var typeGroups = results.GroupBy(r => r.Type).Select(g => $"{g.Key}: {g.Count()}");
                TxtSummary.Text = $"共发现 {results.Count} 个环境 ({string.Join(", ", typeGroups)})";
                TxtStatus.Text = $"扫描完成，共 {results.Count} 项";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"扫描失败: {ex.Message}";
                MessageBox.Show($"扫描失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnScan.IsEnabled = true;
            }
        }
    }
}


