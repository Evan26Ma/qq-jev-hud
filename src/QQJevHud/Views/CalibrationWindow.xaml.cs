using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using QQJevHud.Core;

namespace QQJevHud.Views;

public partial class CalibrationWindow : Window
{
    private System.Windows.Point _start;
    private bool _selecting;

    public event Action<Calibration>? CalibrationSaved;

    public CalibrationWindow(BitmapSource image, ScreenRect windowBounds, uint dpi, Calibration initial)
    {
        InitializeComponent();
        Preview.Source = image;
        var scale = Math.Max(0.75, dpi / 96d);
        Left = windowBounds.Left / scale;
        Top = windowBounds.Top / scale;
        Width = windowBounds.Width / scale;
        Height = windowBounds.Height / scale;
        Loaded += (_, _) => SetSelection(initial.X * ActualWidth, initial.Y * ActualHeight, initial.Width * ActualWidth, initial.Height * ActualHeight);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(SelectionCanvas);
        _selecting = true;
        SelectionCanvas.CaptureMouse();
        SetSelection(_start.X, _start.Y, 1, 1);
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_selecting) return;
        var current = e.GetPosition(SelectionCanvas);
        SetSelection(Math.Min(_start.X, current.X), Math.Min(_start.Y, current.Y), Math.Abs(current.X - _start.X), Math.Abs(current.Y - _start.Y));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _selecting = false;
        SelectionCanvas.ReleaseMouseCapture();
    }

    private void SetSelection(double left, double top, double width, double height)
    {
        System.Windows.Controls.Canvas.SetLeft(Selection, left);
        System.Windows.Controls.Canvas.SetTop(Selection, top);
        Selection.Width = width;
        Selection.Height = height;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (ActualWidth < 1 || ActualHeight < 1 || Selection.Width < 20 || Selection.Height < 20) return;
        CalibrationSaved?.Invoke(new Calibration(
            Math.Clamp(System.Windows.Controls.Canvas.GetLeft(Selection) / ActualWidth, 0, 1),
            Math.Clamp(System.Windows.Controls.Canvas.GetTop(Selection) / ActualHeight, 0, 1),
            Math.Clamp(Selection.Width / ActualWidth, 0.05, 1),
            Math.Clamp(Selection.Height / ActualHeight, 0.05, 1)));
        Close();
    }

    private void CancelClick(object sender, RoutedEventArgs e) => Close();
}
