using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DevEnv.Core.SigXor.Office;
using DevEnv.Host.SigXor.ViewModels;

namespace DevEnv.Host.SigXor.Views;

/// <summary>
/// Office 双引擎预览视图：结构化（Word/Sheet/Slides 三态）与网页（OfficeWebHost）切换。
/// 三个结构化区域以 VM 计算属性 ShowWord/ShowSheet/ShowSlides 驱动可见性（代码后置绑定）；
/// Sheet 的 DataGrid 列按所选工作表在代码后置生成。
/// </summary>
public partial class OfficeView : UserControl
{
    private OfficeContentViewModel? _vm;
    private OfficeWebHost? _webHost;
    private bool _webLoadedFor;

    public static readonly NullToCollapsedConverter NullToCollapsed = new();

    public OfficeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
        Loaded += OnViewLoaded;
    }

    private void OnViewLoaded(object sender, RoutedEventArgs e) => RefreshAll();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = e.NewValue as OfficeContentViewModel;
        if (_vm is null) return;

        _vm.PropertyChanged += OnVmPropertyChanged;
        _webLoadedFor = false;
        if (_vm.LoadCommand.CanExecute(null)) _vm.LoadCommand.Execute(null);
        RefreshAll();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(OfficeContentViewModel.State):
                case nameof(OfficeContentViewModel.Structured):
                case nameof(OfficeContentViewModel.UseWeb):
                case nameof(OfficeContentViewModel.StructuredNotice):
                case nameof(OfficeContentViewModel.LoadingMessage):
                    RefreshAll();
                    break;
            }
        });
    }

    private void RefreshAll()
    {
        if (_vm is null) return;

        // 结构化三态可见性（VM 计算属性 → Visibility）
        BindRegion(WordRegion, nameof(OfficeContentViewModel.ShowWord));
        BindRegion(SheetRegion, nameof(OfficeContentViewModel.ShowSheet));
        BindRegion(SlidesRegion, nameof(OfficeContentViewModel.ShowSlides));

        // 网页视图（常驻 WebView 复用）
        bool wantWeb = _vm.UseWeb && _vm.CanUseWeb;
        WebHostSlot.Visibility = wantWeb ? Visibility.Visible : Visibility.Collapsed;
        if (wantWeb)
        {
            _webHost ??= WebViewHostPool.RentOffice();
            _webHost.DataContext = _vm;
            if (WebHostSlot.Content != _webHost) WebHostSlot.Content = _webHost;
            if (!_webLoadedFor)
            {
                _ = _webHost.LoadAsync(_vm.WebKind, _vm.FilePath);
                _webLoadedFor = true;
            }
        }
        else
        {
            ReleaseWebHost();
        }

        // 加载 / 提示
        LoadingText.Visibility = _vm.State == OfficeContentViewModel.LoadState.Loading
            ? Visibility.Visible : Visibility.Collapsed;

        bool showNotice = _vm.State is OfficeContentViewModel.LoadState.Unsupported
            or OfficeContentViewModel.LoadState.Error;
        NoticePanel.Visibility = showNotice && !_vm.UseWeb ? Visibility.Visible : Visibility.Collapsed;
        NoticeTitle.Text = _vm.State == OfficeContentViewModel.LoadState.Error ? "解析失败" : "此格式无结构化视图";
        NoticeBody.Text = _vm.State == OfficeContentViewModel.LoadState.Error
            ? (_vm.Error ?? "") : (_vm.StructuredNotice ?? "");

        // 已加载但仍有提示（如未装 PowerPoint / 导出失败回退）
        bool softNotice = _vm.State == OfficeContentViewModel.LoadState.Loaded
            && !string.IsNullOrWhiteSpace(_vm.StructuredNotice)
            && !_vm.UseWeb;
        SoftNoticeBanner.Visibility = softNotice ? Visibility.Visible : Visibility.Collapsed;
        SoftNoticeText.Text = softNotice ? _vm.StructuredNotice! : "";

        if (_vm.State == OfficeContentViewModel.LoadState.Loaded) PopulateForModel();
    }

    private void BindRegion(FrameworkElement element, string vmProperty)
    {
        var binding = new Binding(vmProperty)
        {
            Source = _vm,
            Converter = (IValueConverter)FindResource("BoolToVis"),
        };
        element.SetBinding(VisibilityProperty, binding);
    }

    private void PopulateForModel()
    {
        if (_vm?.Structured is not SheetSetModel sheet) return;

        SheetPicker.Items.Clear();
        foreach (var s in sheet.Sheets)
            SheetPicker.Items.Add(s.Name);
        if (SheetPicker.Items.Count > 0)
        {
            SheetPicker.SelectedIndex = 0;
            ShowSheet(sheet.Sheets[0]);
        }
        SheetStats.Text = $"共 {sheet.Sheets.Count} 个工作表 · 合计 {sheet.TotalRows} 行" +
            (sheet.Truncated ? "（已按上限截断显示）" : "");
    }

    private void SheetPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm?.Structured is not SheetSetModel sheet || SheetPicker.SelectedItem is not string name) return;
        var data = sheet.Sheets.FirstOrDefault(s => s.Name == name);
        if (data is not null) ShowSheet(data);
    }

    private void ShowSheet(SheetData data)
    {
        SheetGrid.Columns.Clear();
        for (int i = 0; i < data.Columns.Count; i++)
        {
            int col = i;
            SheetGrid.Columns.Add(new DataGridTextColumn
            {
                Header = data.Columns[i],
                Binding = new Binding($"[{col}]") { Mode = BindingMode.OneWay },
                IsReadOnly = true,
            });
        }
        SheetGrid.ItemsSource = data.Rows;
    }

    private void ReleaseWebHost()
    {
        if (_webHost is null) return;
        if (ReferenceEquals(WebHostSlot.Content, _webHost))
            WebHostSlot.Content = null;
        WebViewHostPool.Return(_webHost);
        _webHost = null;
        _webLoadedFor = false;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ReleaseWebHost();
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
    }
}

/// <summary>null → Collapsed，非 null → Visible。</summary>
public sealed class NullToCollapsedConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
