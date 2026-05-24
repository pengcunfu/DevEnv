using DevEnv.UI;
using Avalonia.Controls;
using Avalonia.Controls;
using DevEnv.Models;
using DevEnv.Services;

namespace DevEnv.Views
{
    public partial class MirrorConfigWindow : Window
    {
        private readonly Dictionary<MirrorToolType, List<MirrorSource>> _mirrorCache = new();
        private MirrorToolType _currentTool = MirrorToolType.Pip;

        private static readonly (MirrorToolType Tool, string Label)[] ToolOptions =
        [
            (MirrorToolType.Pip, "pip (Python)"),
            (MirrorToolType.Npm, "npm (Node.js)"),
            (MirrorToolType.Pnpm, "pnpm (Node.js)"),
            (MirrorToolType.Yarn, "Yarn (Node.js)"),
            (MirrorToolType.Maven, "Maven (Java)"),
            (MirrorToolType.Composer, "Composer (PHP)"),
            (MirrorToolType.Go, "Go 模块代理"),
            (MirrorToolType.NuGet, "NuGet (.NET)")
        ];

        public MirrorConfigWindow()
        {
            InitializeComponent();
            CmbToolType.ItemsSource = ToolOptions.Select(t => t.Label).ToList();
            CmbToolType.SelectedIndex = 0;
            Loaded += async (_, _) => await LoadToolMirrorsAsync();
        }

        private async void CmbToolType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || CmbToolType.SelectedIndex < 0) return;
            _currentTool = ToolOptions[CmbToolType.SelectedIndex].Tool;
            await LoadToolMirrorsAsync();
        }

        private async Task LoadToolMirrorsAsync()
        {
            if (!_mirrorCache.TryGetValue(_currentTool, out var mirrors))
            {
                mirrors = AppServices.MirrorConfig.GetMirrors(_currentTool)
                    .Select(Clone)
                    .ToList();
                _mirrorCache[_currentTool] = mirrors;
            }

            MirrorList.ItemsSource = null;
            MirrorList.ItemsSource = mirrors;

            if (mirrors.Count > 0)
                MirrorList.SelectedIndex = 0;

            var current = await AppServices.MirrorConfig.GetCurrentMirrorAsync(_currentTool);
            var toolName = MirrorConfigService.GetToolDisplayName(_currentTool);
            TxtCurrentMirror.Text = string.IsNullOrEmpty(current)
                ? $"当前 {toolName} 镜像: 未配置（使用官方源）"
                : $"当前 {toolName} 镜像: {current}";
            TxtStatus.Text = $"已加载 {mirrors.Count} 个 {toolName} 镜像源";
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            if (!_mirrorCache.TryGetValue(_currentTool, out var mirrors)) return;

            TxtStatus.Text = "正在测试镜像速度...";
            IsEnabled = false;
            try
            {
                var results = await AppServices.MirrorConfig.TestMirrorSpeedAsync(mirrors);
                _mirrorCache[_currentTool] = results;
                MirrorList.ItemsSource = null;
                MirrorList.ItemsSource = results;

                var fastest = results.FirstOrDefault(m => m.ResponseTimeMs.HasValue);
                TxtStatus.Text = fastest != null
                    ? $"测试完成，推荐: {fastest.Name} ({fastest.ResponseTimeMs:0} ms)"
                    : "测试完成，所有镜像均不可用";
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (MirrorList.SelectedItem is not MirrorSource mirror)
            {
                MessageBox.Show("请先选择一个镜像源", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TxtStatus.Text = $"正在配置 {mirror.Name}...";
            var (success, message) = await AppServices.MirrorConfig.ConfigureMirrorAsync(_currentTool, mirror);
            TxtStatus.Text = message;

            MessageBox.Show(message, success ? "成功" : "失败",
                MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);

            if (success)
                await LoadToolMirrorsAsync();
        }

        private static MirrorSource Clone(MirrorSource source) => new()
        {
            Key = source.Key,
            Name = source.Name,
            Url = source.Url,
            TrustedHost = source.TrustedHost,
            TestUrl = source.TestUrl
        };
    }
}


