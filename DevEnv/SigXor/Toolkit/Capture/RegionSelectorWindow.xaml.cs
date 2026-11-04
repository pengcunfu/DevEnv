using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DevEnv.Core.SigXor.Toolkit;
using DevEnv.Host.SigXor.Services;

namespace DevEnv.Host.SigXor.Toolkit.Capture;

public partial class RegionSelectorWindow : Window
{
    private readonly PixelRect _virtualBounds;
    private readonly double _scale;
    private Point _startDip;
    private Point _endDip;
    private bool _dragging;
    private bool _settled;

    public RegionSelectorWindow(BitmapSource screenshot, PixelRect virtualBounds, double scale)
    {
        InitializeComponent();
        _virtualBounds = virtualBounds;
        _scale = scale > 0 ? scale : 1.0;
        BackgroundImage.Source = screenshot;
        Width = virtualBounds.Width / _scale;
        Height = virtualBounds.Height / _scale;
        SelectionCanvas.Cursor = Cursors.Cross;
        SourceInitialized += (_, _) =>
        {
            WindowPlacement.SetPhysicalBounds(this, _virtualBounds, activate: true);
            Activate();
            SelectionCanvas.Focus();
            PositionHint();
        };
        Loaded += (_, _) => PositionHint();
    }

    public event EventHandler<PixelRect>? SelectionConfirmed;
    public event EventHandler? SelectionCancelled;

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settled) return;

        if (e.ClickCount == 2)
        {
            Confirm(new PixelRect(0, 0, _virtualBounds.Width, _virtualBounds.Height));
            e.Handled = true;
            return;
        }

        _startDip = e.GetPosition(SelectionCanvas);
        _endDip = _startDip;
        _dragging = true;
        SelectionCanvas.CaptureMouse();
        UpdateSelectionVisual();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || _settled) return;
        _endDip = e.GetPosition(SelectionCanvas);
        UpdateSelectionVisual();
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging || _settled) return;

        _dragging = false;
        SelectionCanvas.ReleaseMouseCapture();
        _endDip = e.GetPosition(SelectionCanvas);
        UpdateSelectionVisual();

        PixelRect rect = GetSelectionPhysical();
        if (rect.Width >= 4 && rect.Height >= 4)
        {
            Confirm(rect);
        }
        else
        {
            _startDip = _endDip = default;
            UpdateSelectionVisual();
        }
        e.Handled = true;
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        Cancel();
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cancel();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.Return && !_dragging)
        {
            PixelRect rect = GetSelectionPhysical();
            if (rect.Width >= 4 && rect.Height >= 4)
            {
                Confirm(rect);
                e.Handled = true;
            }
        }
    }

    private PixelRect GetSelectionPhysical()
    {
        double scale = _scale <= 0 ? 1.0 : _scale;
        int x0 = (int)Math.Round(Math.Min(_startDip.X, _endDip.X) * scale);
        int y0 = (int)Math.Round(Math.Min(_startDip.Y, _endDip.Y) * scale);
        int x1 = (int)Math.Round(Math.Max(_startDip.X, _endDip.X) * scale);
        int y1 = (int)Math.Round(Math.Max(_startDip.Y, _endDip.Y) * scale);
        return PixelRect.Normalize(x0, y0, x1, y1);
    }

    private void UpdateSelectionVisual()
    {
        double left = Math.Min(_startDip.X, _endDip.X);
        double top = Math.Min(_startDip.Y, _endDip.Y);
        double width = Math.Abs(_endDip.X - _startDip.X);
        double height = Math.Abs(_endDip.Y - _startDip.Y);
        double canvasWidth = SelectionCanvas.ActualWidth;
        double canvasHeight = SelectionCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0 || width < 1 || height < 1)
        {
            HideSelectionVisuals();
            return;
        }

        SetRect(DimTop, 0, 0, canvasWidth, top);
        SetRect(DimBottom, 0, top + height, canvasWidth, canvasHeight - top - height);
        SetRect(DimLeft, 0, top, left, height);
        SetRect(DimRight, left + width, top, canvasWidth - left - width, height);
        SetRect(SelectionBorder, left, top, width, height);

        PixelRect physical = GetSelectionPhysical();
        SelectionSizeText.Text = $"{physical.Width} × {physical.Height}";
        double labelTop = top - 28;
        if (labelTop < 4) labelTop = top + height + 4;
        Canvas.SetLeft(SelectionLabel, left);
        Canvas.SetTop(SelectionLabel, labelTop);

        DimTop.Visibility = DimBottom.Visibility = DimLeft.Visibility = DimRight.Visibility = Visibility.Visible;
        SelectionBorder.Visibility = SelectionLabel.Visibility = Visibility.Visible;
    }

    private void HideSelectionVisuals()
    {
        DimTop.Visibility = DimBottom.Visibility = DimLeft.Visibility = DimRight.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = SelectionLabel.Visibility = Visibility.Collapsed;
    }

    private static void SetRect(FrameworkElement element, double left, double top, double width, double height)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
    }

    private void PositionHint()
    {
        if (HintText is null || SelectionCanvas is null) return;
        double width = SelectionCanvas.ActualWidth;
        HintText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(HintText, Math.Max(8, (width - HintText.DesiredSize.Width) / 2));
        Canvas.SetTop(HintText, 16);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => PositionHint();

    private void Confirm(PixelRect rect)
    {
        if (_settled) return;
        _settled = true;
        SelectionConfirmed?.Invoke(this, rect);
    }

    private void Cancel()
    {
        if (_settled) return;
        _settled = true;
        SelectionCancelled?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_settled)
        {
            _settled = true;
            SelectionCancelled?.Invoke(this, EventArgs.Empty);
        }
        base.OnClosed(e);
    }
}
