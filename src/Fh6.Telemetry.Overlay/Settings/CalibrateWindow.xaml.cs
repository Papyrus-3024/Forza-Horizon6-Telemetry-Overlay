using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Fh6.Telemetry.Core;
using Fh6.Telemetry.Overlay.ViewModels;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Overlay.Settings;

public partial class CalibrateWindow : Window
{
    private const double DisplaySize = 800.0;

    private readonly TelemetryViewModel _vm;
    private readonly OverlayConfig _config;
    private int _sourcePixelSize;

    // Pending capture — set when user clicks "Capture car position", cleared on map click.
    private (double worldX, double worldZ)? _pending;

    private readonly List<(double worldX, double worldZ, double pixelX, double pixelY)> _points = new();

    public CalibrateWindow(TelemetryViewModel vm, OverlayConfig config)
    {
        InitializeComponent();
        _vm = vm;
        _config = config;

        LoadMap();
    }

    private void LoadMap()
    {
        var path = MapImageResolver.Resolve(_config);
        if (path is null)
        {
            StatusLabel.Text = "No map image for the current season.";
            CaptureBtn.IsEnabled = false;
            MapImage.IsEnabled = false;
            return;
        }

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();

        MapImage.Source = bmp;
        _sourcePixelSize = bmp.PixelWidth;   // square map — width == height
    }

    // ── Capture ──────────────────────────────────────────────────────────────

    private void OnCapture(object sender, RoutedEventArgs e)
    {
        _pending = (_vm.WorldX, _vm.WorldZ);
        StatusLabel.Text =
            $"Captured world ({_pending.Value.worldX:F1}, {_pending.Value.worldZ:F1}). " +
            "Now click that exact spot on the map.";
    }

    // ── Map click ────────────────────────────────────────────────────────────

    private void OnMapClick(object sender, MouseButtonEventArgs e)
    {
        if (_pending is null) return;
        if (_sourcePixelSize <= 0) return;

        var click = e.GetPosition(MapImage);
        double scale = _sourcePixelSize / DisplaySize;
        double srcX = click.X * scale;
        double srcY = click.Y * scale;

        var pt = (_pending.Value.worldX, _pending.Value.worldZ, srcX, srcY);
        _points.Add(pt);
        _pending = null;

        PlaceDot(click.X, click.Y, _points.Count);
        RefreshList();
        StatusLabel.Text = $"Point {_points.Count} added. Add more or Fit & Save.";
    }

    private void PlaceDot(double x, double y, int label)
    {
        const double r = 5.0;
        var circle = new Ellipse
        {
            Width = r * 2,
            Height = r * 2,
            Fill = Brushes.OrangeRed,
            Stroke = Brushes.White,
            StrokeThickness = 1,
        };
        Canvas.SetLeft(circle, x - r);
        Canvas.SetTop(circle, y - r);

        var text = new TextBlock
        {
            Text = label.ToString(),
            FontSize = 10,
            Foreground = Brushes.White,
        };
        Canvas.SetLeft(text, x + r + 1);
        Canvas.SetTop(text, y - r);

        DotCanvas.Children.Add(circle);
        DotCanvas.Children.Add(text);
    }

    // ── List / buttons ───────────────────────────────────────────────────────

    private void RefreshList()
    {
        PointsList.ItemsSource = null;
        PointsList.ItemsSource = _points
            .Select((p, i) => $"{i + 1,2}. world({p.worldX,9:F1}, {p.worldZ,9:F1})  px({p.pixelX,7:F1}, {p.pixelY,7:F1})")
            .ToList();

        FitSaveBtn.IsEnabled = _points.Count >= 3;
    }

    private void OnRemoveLast(object sender, RoutedEventArgs e)
    {
        if (_points.Count == 0) return;
        _points.RemoveAt(_points.Count - 1);

        // Rebuild dots from scratch (simpler than tracking individual elements).
        DotCanvas.Children.Clear();
        for (int i = 0; i < _points.Count; i++)
        {
            var p = _points[i];
            double displayX = _sourcePixelSize > 0
                ? p.pixelX / (_sourcePixelSize / DisplaySize)
                : p.pixelX;
            double displayY = _sourcePixelSize > 0
                ? p.pixelY / (_sourcePixelSize / DisplaySize)
                : p.pixelY;
            PlaceDot(displayX, displayY, i + 1);
        }

        RefreshList();
        _pending = null;
        StatusLabel.Text = _points.Count > 0 ? $"{_points.Count} point(s) remaining." : string.Empty;
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        _points.Clear();
        DotCanvas.Children.Clear();
        _pending = null;
        RefreshList();
        StatusLabel.Text = string.Empty;
    }

    private void OnFitSave(object sender, RoutedEventArgs e)
    {
        try
        {
            _config.MapCalibration = AffineFit.Fit(_points);
            ConfigStore.Save(ConfigStore.DefaultPath, _config);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Fit failed: {ex.Message}";
        }
    }
}
