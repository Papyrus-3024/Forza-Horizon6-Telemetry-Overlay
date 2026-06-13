using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Fh6.Telemetry.Core;

namespace Fh6.Telemetry.Overlay.Widgets;

public partial class MapWidget : UserControl
{
    // ── DependencyProperties ────────────────────────────────────────────────

    public static readonly DependencyProperty WorldXProperty =
        DependencyProperty.Register(
            nameof(WorldX),
            typeof(double),
            typeof(MapWidget),
            new PropertyMetadata(0.0, OnWorldCoordChanged));

    public static readonly DependencyProperty WorldZProperty =
        DependencyProperty.Register(
            nameof(WorldZ),
            typeof(double),
            typeof(MapWidget),
            new PropertyMetadata(0.0, OnWorldCoordChanged));

    public double WorldX
    {
        get => (double)GetValue(WorldXProperty);
        set => SetValue(WorldXProperty, value);
    }

    public double WorldZ
    {
        get => (double)GetValue(WorldZProperty);
        set => SetValue(WorldZProperty, value);
    }

    // ── Private state ───────────────────────────────────────────────────────

    private BitmapImage? _bitmap;   // null when image failed to load or no path
    private MapCalibration? _cal;

    // ── Constructor ─────────────────────────────────────────────────────────

    public MapWidget() => InitializeComponent();

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Loads (or reloads) the map image and stores the calibration.
    /// Safe to call with a null or missing path: the widget degrades gracefully.
    /// </summary>
    public void Configure(string? imagePath, MapCalibration? cal)
    {
        _cal = cal;
        _bitmap = TryLoadBitmap(imagePath);

        if (_bitmap is not null)
        {
            MapImage.Source = _bitmap;

            // Show calibration hint when image loaded but cal is absent.
            if (_cal is null)
                ShowPlaceholder("Map not calibrated\nPress F11 to calibrate");
            else
                HidePlaceholder();
        }
        else
        {
            MapImage.Source = null;
            ShowPlaceholder("No map image");
        }

        UpdateMarker();
    }

    // ── Event handlers ──────────────────────────────────────────────────────

    private static void OnWorldCoordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MapWidget)d).UpdateMarker();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateMarker();

    // ── Marker positioning ──────────────────────────────────────────────────

    private void UpdateMarker()
    {
        if (_bitmap is null || _cal is null)
        {
            Marker.Visibility = Visibility.Collapsed;
            return;
        }

        // Source image dimensions — never assume a fixed size.
        double srcW = _bitmap.PixelWidth;
        double srcH = _bitmap.PixelHeight;
        if (srcW <= 0 || srcH <= 0)
        {
            Marker.Visibility = Visibility.Collapsed;
            return;
        }

        // Available canvas size (the Overlay Canvas fills the Grid cell).
        double canvasW = Overlay.ActualWidth;
        double canvasH = Overlay.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0)
        {
            Marker.Visibility = Visibility.Collapsed;
            return;
        }

        // Convert world coords to source-image pixel coords.
        var (pixX, pixY) = WorldToMap.ToPixel(WorldX, WorldZ, _cal);
        if (double.IsNaN(pixX) || double.IsNaN(pixY) ||
            double.IsInfinity(pixX) || double.IsInfinity(pixY))
        {
            Marker.Visibility = Visibility.Collapsed;
            return;
        }

        // Scale source-image pixel to displayed canvas coords.
        // Image.Stretch=Uniform: the image is letter-boxed to fit within the canvas.
        // Compute the actual rendered rect of the image inside the canvas.
        double scaleX = canvasW / srcW;
        double scaleY = canvasH / srcH;
        double scale  = Math.Min(scaleX, scaleY);   // Uniform uses the smaller scale

        double renderedW = srcW * scale;
        double renderedH = srcH * scale;
        double offsetX   = (canvasW - renderedW) / 2.0;  // letter-box padding
        double offsetY   = (canvasH - renderedH) / 2.0;

        double displayX = offsetX + pixX * scale;
        double displayY = offsetY + pixY * scale;

        // Clamp to the rendered image rect so the dot never leaves the map.
        displayX = Math.Clamp(displayX, offsetX, offsetX + renderedW);
        displayY = Math.Clamp(displayY, offsetY, offsetY + renderedH);

        double half = Marker.Width / 2.0;
        Canvas.SetLeft(Marker, displayX - half);
        Canvas.SetTop(Marker,  displayY - half);
        Marker.Visibility = Visibility.Visible;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static BitmapImage? TryLoadBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            if (!File.Exists(path)) return null;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource           = new Uri(path, UriKind.Absolute);
            bmp.CacheOption         = BitmapCacheOption.OnLoad;
            bmp.CreateOptions       = BitmapCreateOptions.IgnoreImageCache;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            // Any I/O, format, or access error: degrade to placeholder.
            return null;
        }
    }

    private void ShowPlaceholder(string text)
    {
        Placeholder.Text = text;
        Placeholder.Visibility = Visibility.Visible;
    }

    private void HidePlaceholder()
    {
        Placeholder.Visibility = Visibility.Collapsed;
    }
}
