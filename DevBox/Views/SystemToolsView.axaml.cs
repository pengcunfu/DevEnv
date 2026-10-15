using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ProgramBox.Converters;
using ProgramBox.Models;
using ProgramBox.Utils;

namespace ProgramBox.Views
{
    public partial class SystemToolsView : UserControl
    {
        private readonly JsonDataManager _dataManager;
        private readonly ObservableCollection<WinToolAtom> _allTools;

        public SystemToolsView()
            : this(new JsonDataManager())
        {
        }

        public SystemToolsView(JsonDataManager dataManager)
        {
            InitializeComponent();
            _dataManager = dataManager;
            _allTools = new ObservableCollection<WinToolAtom>(_dataManager.Data.WinToolAtomList);
            
            OrganizeToolsByCategory();
        }

        /// <summary>
        /// 按分类组织工具
        /// </summary>
        private void OrganizeToolsByCategory()
        {
            // 系统管理工具
            var systemManagementTools = new[]
            {
                ("computer", "计算机管理"),
                ("server", "服务"),
                ("config", "系统配置"),
                ("startup", "启动项管理"),
                ("regedit", "注册表"),
                ("env", "环境变量")
            };

            // 硬件与设备工具
            var hardwareTools = new[]
            {
                ("device", "设备管理器"),
                ("disk", "磁盘管理"),
                ("sound", "声音设置"),
                ("display", "显示设置"),
                ("power", "电源选项")
            };

            // 网络与安全工具
            var networkSecurityTools = new[]
            {
                ("firewall", "防火墙"),
                ("security", "本地安全策略"),
                ("network", "网络连接"),
                ("uac", "用户账户控制")
            };

            // 性能与监控工具
            var performanceTools = new[]
            {
                ("task", "任务管理器"),
                ("res", "资源监视器"),
                ("event", "事件查看器"),
                ("info", "系统信息"),
                ("planned_task", "计划任务")
            };

            CreateToolCards(SystemManagementPanel, systemManagementTools);
            CreateToolCards(HardwarePanel, hardwareTools);
            CreateToolCards(NetworkSecurityPanel, networkSecurityTools);
            CreateToolCards(PerformancePanel, performanceTools);
        }

        /// <summary>
        /// 创建工具卡片
        /// </summary>
        private void CreateToolCards(Panel panel, (string key, string name)[] tools)
        {
            panel.Children.Clear();
            
            foreach (var (key, name) in tools)
            {
                var tool = _allTools.FirstOrDefault(t => t.Key == key);
                if (tool != null)
                {
                    var card = CreateToolCard(tool);
                    panel.Children.Add(card);
                }
            }
        }

        /// <summary>
        /// 创建单个工具卡片
        /// </summary>
        private Border CreateToolCard(WinToolAtom tool)
        {
            var card = new Border
            {
                Classes = { "ToolCard" },
                Tag = tool.Key,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            card.PointerPressed += ToolCard_Click;

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconHost = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(6),
                Background = GetIconBackgroundBrush(),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var icon = new Image
            {
                Width = 28,
                Height = 28,
                Stretch = Stretch.Uniform,
                Source = LoadToolIcon(tool)
            };

            iconHost.Child = icon;

            var text = new TextBlock
            {
                Text = tool.Name,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 88
            };

            stackPanel.Children.Add(iconHost);
            stackPanel.Children.Add(text);
            card.Child = stackPanel;

            return card;
        }

        /// <summary>
        /// 获取图标占位背景色（跟随深浅色主题）
        /// </summary>
        private static IBrush GetIconBackgroundBrush()
        {
            var app = Application.Current;
            if (app != null
                && app.TryFindResource("IconBackgroundBrush", app.RequestedThemeVariant, out var resource)
                && resource is IBrush brush)
            {
                return brush;
            }

            return new SolidColorBrush(Color.Parse("#EEEEEE"));
        }

        /// <summary>
        /// 加载工具图标，缺失时回退到默认图标
        /// </summary>
        private static IImage? LoadToolIcon(WinToolAtom tool)
        {
            var converter = PathToBitmapConverter.Instance;
            var icon = converter.Convert(tool.IconPath, typeof(IImage), null, System.Globalization.CultureInfo.InvariantCulture) as Bitmap;
            if (icon != null)
                return icon;

            return converter.Convert("/Assets/Icons/win/default.png", typeof(IImage), null, System.Globalization.CultureInfo.InvariantCulture) as Bitmap;
        }

        #region 事件处理

        private void ToolCard_Click(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            var requireDouble = _dataManager.Data.Config.DoubleClickToLaunch;
            if (requireDouble ? e.ClickCount < 2 : e.ClickCount != 1)
                return;

            if (sender is Border border && border.Tag is string key)
            {
                ExecuteSystemTool(key);
            }
        }

        private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text?.ToLower() ?? string.Empty;
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                OrganizeToolsByCategory();
            }
            else
            {
                FilterTools(searchText);
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 过滤工具
        /// </summary>
        private void FilterTools(string searchText)
        {
            var filtered = _allTools.Where(t => 
                t.Name.ToLower().Contains(searchText) || 
                t.Key.ToLower().Contains(searchText) ||
                t.Description.ToLower().Contains(searchText)).ToArray();

            SystemManagementPanel.Children.Clear();
            HardwarePanel.Children.Clear();
            NetworkSecurityPanel.Children.Clear();
            PerformancePanel.Children.Clear();

            foreach (var tool in filtered)
            {
                var card = CreateToolCard(tool);
                SystemManagementPanel.Children.Add(card);
            }
        }

        /// <summary>
        /// 执行系统工具
        /// </summary>
        private void ExecuteSystemTool(string key)
        {
            try
            {
                if (string.Equals(key, "startup", StringComparison.OrdinalIgnoreCase))
                {
                    var owner = TopLevel.GetTopLevel(this) as Window;
                    StartupManagerWindow.ShowOrActivate(owner);
                    return;
                }

                WindowsToolsHelper.ExecuteWindowsTool(key);

                // 更新状态
                var tool = _allTools.FirstOrDefault(t => t.Key == key);
                if (tool != null)
                {
                    _ = AppHelper.ShowInfoAsync($"正在启动 {tool.Name}...");
                }
            }
            catch (Exception ex)
            {
                _ = AppHelper.ShowErrorAsync($"启动工具失败: {ex.Message}");
            }
        }

        #endregion
    }
}
