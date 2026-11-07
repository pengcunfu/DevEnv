using System.Windows;
using System.Windows.Input;
using DevEnv.Host.SigXor.ViewModels;

namespace DevEnv.Host.SigXor.Views;

public partial class ShellPreviewWindow : Window
{
    private const double MiniWidth = 420;
    private const double MiniHeight = 300;
    private const double MiniMargin = 12;

    private bool _isMini;
    private double _normalWidth;
    private double _normalHeight;
    private double _normalLeft;
    private double _normalTop;
    private WindowState _normalState;
    private bool _topmostBeforeMini;

    public ShellPreviewWindow()
    {
        InitializeComponent();
    }

    private ShellPreviewViewModel? Vm => DataContext as ShellPreviewViewModel;

    /// <summary>
    /// 检查当前内容是否处于编辑模式
    /// </summary>
    public bool IsInEditMode()
    {
        if (Vm?.Preview?.Content is TextContentViewModel textVm)
        {
            return textVm.IsEditMode;
        }
        return false;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 编辑模式或输入框里不拦截按键，让编辑器 / 输入控件处理方向键
        if (IsInEditMode()
            || Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                ClosePreview();
                break;
            case Key.Down:
            case Key.Right:
                e.Handled = true;
                Vm?.NextCommand.Execute(null);
                break;
            case Key.Up:
            case Key.Left:
                e.Handled = true;
                Vm?.PreviousCommand.Execute(null);
                break;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => ClosePreview();

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void OnTogglePin(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePinButton();
    }

    /// <summary>小窗模式：固定到屏幕右上角并缩小窗口，再点一次恢复原来的大小与位置。</summary>
    private void OnToggleMini(object sender, RoutedEventArgs e)
    {
        if (_isMini)
        {
            ExitMini();
        }
        else
        {
            EnterMini();
        }
    }

    private void EnterMini()
    {
        if (_isMini) return;

        _normalWidth = Width;
        _normalHeight = Height;
        _normalLeft = Left;
        _normalTop = Top;
        _normalState = WindowState;
        _topmostBeforeMini = Topmost;

        _isMini = true;
        WindowState = WindowState.Normal;
        Width = MiniWidth;
        Height = MiniHeight;
        Topmost = true;
        UpdatePinButton();

        // WorkArea 是屏幕工作区（不含任务栏），坐标单位为 DIP
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - MiniMargin;
        Top = workArea.Top + MiniMargin;

        // 小窗空间有限：收起键盘提示和置顶按钮，只留左右切换与关闭
        KeyHintText.Visibility = Visibility.Collapsed;
        PinButton.Visibility = Visibility.Collapsed;
        MiniButton.Content = "还原";
        MiniButton.ToolTip = "退出小窗模式，恢复原来的大小与位置";
    }

    private void ExitMini()
    {
        if (!_isMini) return;

        _isMini = false;
        WindowState = WindowState.Normal;
        Width = _normalWidth;
        Height = _normalHeight;
        WindowState = _normalState;
        Left = _normalLeft;
        Top = _normalTop;
        Topmost = _topmostBeforeMini;
        UpdatePinButton();

        KeyHintText.Visibility = Visibility.Visible;
        PinButton.Visibility = Visibility.Visible;
        MiniButton.Content = "小窗";
        MiniButton.ToolTip = "小窗模式：固定到右上角并缩小窗口";
    }

    private void UpdatePinButton()
    {
        if (Topmost)
        {
            PinButton.Content = "\uE840"; // 置顶图标
            PinButton.ToolTip = "取消置顶";
        }
        else
        {
            PinButton.Content = "\uE718"; // 非置顶图标
            PinButton.ToolTip = "切换置顶";
        }
    }

    public void ClosePreview()
    {
        // 延迟到当前事件处理完成后执行，避免在键盘/鼠标事件期间销毁编辑器
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                Vm?.DisposeContent();
            }
            catch
            {
                // 忽略清理异常，避免阻止窗口关闭
            }
            Hide();
        });
    }
}
